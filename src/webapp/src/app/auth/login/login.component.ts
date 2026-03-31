import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { firstValueFrom } from 'rxjs';
import { ExternalProvider } from '../models/auth-session.model';
import { AuthApiService } from '../services/auth-api.service';
import { AuthSessionStore } from '../services/auth-session.store';
import { environment } from '../../../enviroments/enviroment';

type OtpPhase = 'request' | 'verify';

const otpUiLimits = {
  ttlMinutes: 10,
  maxVerifyAttempts: 5,
  maxRequestsPerWindowPerEmail: 3,
  requestWindowMinutes: 15,
  minimumSecondsBetweenRequestsPerEmail: 30,
} as const;

type ValidationErrors = Record<string, string[] | string>;

type ValidationProblemDetails = {
  errors?: ValidationErrors;
};

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatDividerModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
})
export class LoginComponent implements OnInit, OnDestroy {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApiService = inject(AuthApiService);
  private readonly authSessionStore = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);

  readonly isBusy = signal(false);
  readonly usePasswordLogin = signal(false);
  readonly otpPhase = signal<OtpPhase>('request');
  readonly externalProviders = signal<ExternalProvider[]>([]);
  readonly passwordError = signal<string | null>(null);
  readonly passwordSuccessMessage = signal<string | null>(null);
  readonly otpRequestMessage = signal<string | null>(null);
  readonly otpError = signal<string | null>(null);
  readonly otpVerifyAttemptsRemaining = signal<number>(otpUiLimits.maxVerifyAttempts);
  readonly otpResendCooldownSeconds = signal<number>(0);
  readonly otpRules = otpUiLimits;

  readonly passwordForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  readonly otpForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    code: ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
  });
  private resendCooldownInterval: ReturnType<typeof setInterval> | null = null;
  private otpRequestTimestampsByEmail = new Map<string, number[]>();
  private otpCooldownEmail: string | null = null;
  private otpIssuedAtMs: number | null = null;

  async ngOnInit(): Promise<void> {
    await this.loadExternalProviders();
  }

  ngOnDestroy(): void {
    this.clearResendCooldownTimer();
  }

  async submitPasswordLogin(): Promise<void> {
    const emailControl = this.passwordForm.controls.email;
    const passwordControl = this.passwordForm.controls.password;

    if (emailControl.invalid || passwordControl.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { email, password } = this.passwordForm.getRawValue();

    this.isBusy.set(true);
    this.passwordError.set(null);
    this.passwordSuccessMessage.set(null);

    try {
      await firstValueFrom(this.authApiService.loginPassword(email, password));
      await this.authSessionStore.refreshSession();
      await this.router.navigate(['/']);
    } catch (error: unknown) {
      const loginStatus = this.getErrorStatus(error);
      if (loginStatus !== 401) {
        this.passwordError.set('Could not sign in right now. Please try again.');
        return;
      }

      try {
        await firstValueFrom(this.authApiService.register(email, password));
        this.passwordSuccessMessage.set('Account created. Confirm your email from inbox, then continue.');
        this.otpForm.controls.email.setValue(email);
        this.passwordForm.controls.password.setValue('');
      } catch (registrationError: unknown) {
        this.passwordError.set(this.buildRegistrationErrorMessage(registrationError));
      }
    } finally {
      this.isBusy.set(false);
    }
  }

  toggleLoginMode(): void {
    const nextMode = !this.usePasswordLogin();
    this.usePasswordLogin.set(nextMode);

    if (nextMode)
    {
      const email = this.otpForm.controls.email.getRawValue();
      if (email)
      {
        this.passwordForm.controls.email.setValue(email);
      }
    }
    else
    {
      const email = this.passwordForm.controls.email.getRawValue();
      if (email)
      {
        this.otpForm.controls.email.setValue(email);
      }
    }

    this.passwordError.set(null);
    this.passwordSuccessMessage.set(null);
    this.otpError.set(null);
    this.otpRequestMessage.set(null);
  }

  async requestOtp(): Promise<void> {
    const emailControl = this.otpForm.controls.email;
    if (emailControl.invalid) {
      emailControl.markAsTouched();
      return;
    }

    const { email } = this.otpForm.getRawValue();
    const trimmedEmail = email.trim();
    if (!this.canRequestOtpForEmail(trimmedEmail)) {
      return;
    }

    this.isBusy.set(true);
    this.otpError.set(null);
    this.otpRequestMessage.set(null);

    try {
      await firstValueFrom(this.authApiService.requestOtp(trimmedEmail));
      this.otpRequestMessage.set('If this email is eligible, we sent a 6-digit sign-in code.');
      this.otpPhase.set('verify');
      this.otpVerifyAttemptsRemaining.set(otpUiLimits.maxVerifyAttempts);
      this.otpIssuedAtMs = Date.now();
      this.otpForm.controls.email.setValue(trimmedEmail);
      this.otpForm.controls.code.setValue('');
      this.trackOtpRequestTimestamp(trimmedEmail);
      this.startResendCooldownTimer(trimmedEmail);
    } catch {
      this.otpError.set('Could not send a code right now. Please try again.');
    } finally {
      this.isBusy.set(false);
    }
  }

  async verifyOtp(): Promise<void> {
    if (this.otpPhase() !== 'verify') {
      return;
    }

    const emailControl = this.otpForm.controls.email;
    const codeControl = this.otpForm.controls.code;

    if (emailControl.invalid) {
      this.otpError.set('Enter a valid email to continue.');
      this.moveToOtpRequestPhase();
      return;
    }

    if (codeControl.invalid) {
      codeControl.markAsTouched();
      return;
    }

    if (this.isOtpCodeExpired()) {
      this.otpError.set('This code expired. Request a new one.');
      this.moveToOtpRequestPhase();
      return;
    }

    const email = emailControl.getRawValue().trim();
    const code = codeControl.getRawValue();

    this.isBusy.set(true);
    this.otpError.set(null);

    try {
      await firstValueFrom(this.authApiService.verifyOtp(email, code));
      await this.authSessionStore.refreshSession();
      await this.router.navigate(['/']);
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        const attemptsLeft = Math.max(0, this.otpVerifyAttemptsRemaining() - 1);
        this.otpVerifyAttemptsRemaining.set(attemptsLeft);
        if (attemptsLeft === 0) {
          this.otpError.set('Too many incorrect attempts. Request a new code.');
          this.moveToOtpRequestPhase();
        } else {
          this.otpError.set(`That code did not work. ${attemptsLeft} attempt(s) left.`);
        }
      } else {
        this.otpError.set('Could not verify the code right now. Please try again.');
      }
    } finally {
      this.isBusy.set(false);
    }
  }

  useAnotherEmailForOtp(): void {
    this.moveToOtpRequestPhase();
    this.otpForm.controls.email.markAsUntouched();
    this.otpForm.controls.code.setValue('');
    this.otpRequestMessage.set(null);
    this.otpError.set(null);
  }

  isOtpResendBlocked(): boolean {
    if (this.otpResendCooldownSeconds() > 0) {
      return true;
    }

    const normalizedEmail = this.normalizeEmail(this.otpForm.controls.email.getRawValue());
    if (!normalizedEmail) {
      return false;
    }

    return this.getRecentOtpRequestCount(normalizedEmail) >= otpUiLimits.maxRequestsPerWindowPerEmail;
  }

  startExternalLogin(providerId: string): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const callbackUrl = new URL('/auth/callback', window.location.origin).toString();
    const usersApiUrl = environment.usersApiUrl.replace(/\/$/, '');
    const target = `${usersApiUrl}/users/auth/external/${encodeURIComponent(providerId)}/start?returnUrl=${encodeURIComponent(callbackUrl)}`;
    window.location.assign(target);
  }

  private async loadExternalProviders(): Promise<void> {
    try {
      const providers = await firstValueFrom(this.authApiService.externalProviders());
      this.externalProviders.set(providers);
    } catch {
      this.externalProviders.set([]);
    }
  }

  private canRequestOtpForEmail(email: string): boolean {
    const normalizedEmail = this.normalizeEmail(email);
    if (!normalizedEmail) {
      this.otpError.set('Enter a valid email.');
      return false;
    }

    if (this.otpCooldownEmail === normalizedEmail && this.otpResendCooldownSeconds() > 0) {
      this.otpError.set(`Please wait ${this.otpResendCooldownSeconds()} second(s) before requesting another code.`);
      return false;
    }

    this.pruneOtpRequestWindow(normalizedEmail);
    const requests = this.otpRequestTimestampsByEmail.get(normalizedEmail) ?? [];
    if (requests.length >= otpUiLimits.maxRequestsPerWindowPerEmail) {
      const oldestTimestamp = requests[0];
      const retryAtMs = oldestTimestamp + (otpUiLimits.requestWindowMinutes * 60 * 1000);
      const secondsRemaining = Math.max(1, Math.ceil((retryAtMs - Date.now()) / 1000));
      const minutesRemaining = Math.ceil(secondsRemaining / 60);
      this.otpError.set(`Too many code requests. Try again in about ${minutesRemaining} minute(s).`);
      return false;
    }

    return true;
  }

  private normalizeEmail(email: string): string | null {
    const normalized = email.trim().toLowerCase();
    return normalized || null;
  }

  private getRecentOtpRequestCount(normalizedEmail: string): number {
    const windowMs = otpUiLimits.requestWindowMinutes * 60 * 1000;
    const cutoff = Date.now() - windowMs;
    const requests = this.otpRequestTimestampsByEmail.get(normalizedEmail) ?? [];
    return requests.filter((timestamp) => timestamp >= cutoff).length;
  }

  private pruneOtpRequestWindow(normalizedEmail: string): void {
    const windowMs = otpUiLimits.requestWindowMinutes * 60 * 1000;
    const cutoff = Date.now() - windowMs;
    const requests = this.otpRequestTimestampsByEmail.get(normalizedEmail) ?? [];
    const filteredRequests = requests.filter((timestamp) => timestamp >= cutoff);

    if (filteredRequests.length === 0) {
      this.otpRequestTimestampsByEmail.delete(normalizedEmail);
      return;
    }

    this.otpRequestTimestampsByEmail.set(normalizedEmail, filteredRequests);
  }

  private trackOtpRequestTimestamp(email: string): void {
    const normalizedEmail = this.normalizeEmail(email);
    if (!normalizedEmail) {
      return;
    }

    this.pruneOtpRequestWindow(normalizedEmail);

    const requests = this.otpRequestTimestampsByEmail.get(normalizedEmail) ?? [];
    requests.push(Date.now());
    this.otpRequestTimestampsByEmail.set(normalizedEmail, requests);
  }

  private startResendCooldownTimer(email: string): void {
    const normalizedEmail = this.normalizeEmail(email);
    if (!normalizedEmail) {
      return;
    }

    this.clearResendCooldownTimer();
    this.otpCooldownEmail = normalizedEmail;
    this.otpResendCooldownSeconds.set(otpUiLimits.minimumSecondsBetweenRequestsPerEmail);

    this.resendCooldownInterval = setInterval(() => {
      const next = this.otpResendCooldownSeconds() - 1;
      if (next <= 0) {
        this.otpResendCooldownSeconds.set(0);
        this.otpCooldownEmail = null;
        this.clearResendCooldownTimer();
        return;
      }

      this.otpResendCooldownSeconds.set(next);
    }, 1000);
  }

  private clearResendCooldownTimer(): void {
    if (this.resendCooldownInterval) {
      clearInterval(this.resendCooldownInterval);
      this.resendCooldownInterval = null;
    }
  }

  private moveToOtpRequestPhase(): void {
    this.otpPhase.set('request');
    this.otpForm.controls.code.setValue('');
    this.otpVerifyAttemptsRemaining.set(otpUiLimits.maxVerifyAttempts);
    this.otpIssuedAtMs = null;
  }

  private isOtpCodeExpired(): boolean {
    if (!this.otpIssuedAtMs) {
      return true;
    }

    const ttlMs = otpUiLimits.ttlMinutes * 60 * 1000;
    return Date.now() - this.otpIssuedAtMs > ttlMs;
  }

  private getErrorStatus(error: unknown): number | null {
    if (error instanceof HttpErrorResponse) {
      return error.status;
    }

    if (typeof error === 'object' && error !== null && 'status' in error) {
      const status = (error as { status?: unknown }).status;
      if (typeof status === 'number') {
        return status;
      }
    }

    return null;
  }

  private buildRegistrationErrorMessage(error: unknown): string {
    const genericMessage = 'Could not sign in. If the account already exists, confirm your email and try again.';
    const validationMessages = this.extractValidationMessages(error);

    if (validationMessages.length > 0) {
      return validationMessages.join('\n');
    }

    return genericMessage;
  }

  private extractValidationMessages(error: unknown): string[] {
    const payload = this.getErrorPayload(error);
    if (!payload || typeof payload !== 'object' || !('errors' in payload)) {
      return [];
    }

    const errors = (payload as ValidationProblemDetails).errors;
    if (!errors || typeof errors !== 'object') {
      return [];
    }

    const messages: string[] = [];
    for (const value of Object.values(errors)) {
      if (Array.isArray(value)) {
        for (const item of value) {
          if (typeof item === 'string' && item.trim().length > 0) {
            messages.push(item.trim());
          }
        }
        continue;
      }

      if (typeof value === 'string' && value.trim().length > 0) {
        messages.push(value.trim());
      }
    }

    return Array.from(new Set(messages));
  }

  private getErrorPayload(error: unknown): unknown {
    if (error instanceof HttpErrorResponse) {
      return error.error;
    }

    if (typeof error === 'object' && error !== null && 'error' in error) {
      return (error as { error?: unknown }).error;
    }

    return null;
  }
}

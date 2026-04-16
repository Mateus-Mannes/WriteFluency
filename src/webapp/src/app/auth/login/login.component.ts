import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
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
import { Insights, InsightsMeasurements } from '../../../telemetry/insights.service';

type OtpPhase = 'request' | 'verify';

const otpUiLimits = {
  ttlMinutes: 10,
  maxVerifyAttempts: 5,
  maxRequestsPerWindowPerEmail: 3,
  requestWindowMinutes: 15,
  minimumSecondsBetweenRequestsPerEmail: 30,
} as const;
const postLoginReturnUrlStorageKey = 'wf.auth.post-login-return-url.v1';
const postLoginSourceStorageKey = 'wf.auth.post-login-source.v1';
const defaultPostLoginRoute = '/user';

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
  private readonly route = inject(ActivatedRoute);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly insights = inject(Insights, { optional: true });
  private readonly isBrowser = isPlatformBrowser(this.platformId);
  private postLoginReturnUrl: string | null = null;
  private loginSource = 'direct';

  readonly isBusy = signal(false);
  readonly usePasswordLogin = signal(false);
  readonly otpPhase = signal<OtpPhase>('request');
  readonly externalProviders = signal<ExternalProvider[]>([]);
  readonly passwordError = signal<string | null>(null);
  readonly passwordSuccessMessage = signal<string | null>(null);
  readonly awaitingEmailConfirmation = signal<string | null>(null);
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
    this.postLoginReturnUrl = this.resolveReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
    this.loginSource = this.resolveSource(this.route.snapshot.queryParamMap.get('source'));
    this.persistPostLoginReturnUrl(this.postLoginReturnUrl);
    this.persistPostLoginSource(this.loginSource);
    this.trackAuthEvent('auth_login_page_viewed', {
      source: this.loginSource,
      has_return_url: Boolean(this.postLoginReturnUrl),
      return_url: this.postLoginReturnUrl,
    });
    await this.loadExternalProviders();
  }

  ngOnDestroy(): void {
    this.clearResendCooldownTimer();
  }

  async submitPasswordLogin(): Promise<void> {
    await this.handlePasswordLoginAttempt(true);
  }

  async continueAfterConfirmation(): Promise<void> {
    await this.handlePasswordLoginAttempt(false);
  }

  isAwaitingEmailConfirmationForCurrentEmail(): boolean {
    const awaitingEmail = this.awaitingEmailConfirmation();
    const currentEmail = this.normalizeEmail(this.passwordForm.controls.email.getRawValue());

    return awaitingEmail !== null && currentEmail === awaitingEmail;
  }

  private async handlePasswordLoginAttempt(allowAutoRegistration: boolean): Promise<void> {
    const emailControl = this.passwordForm.controls.email;
    const passwordControl = this.passwordForm.controls.password;

    if (emailControl.invalid || passwordControl.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const email = emailControl.getRawValue().trim();
    const password = passwordControl.getRawValue();
    emailControl.setValue(email);
    this.trackAuthEvent('auth_password_login_attempted', {
      source: this.loginSource,
      allow_auto_registration: allowAutoRegistration,
    });

    this.isBusy.set(true);
    this.passwordError.set(null);
    if (allowAutoRegistration) {
      this.passwordSuccessMessage.set(null);
    }

    try {
      await firstValueFrom(this.authApiService.loginPassword(email, password));
      this.awaitingEmailConfirmation.set(null);
      this.passwordSuccessMessage.set(null);
      this.trackAuthEvent('auth_login_succeeded', {
        method: 'password',
        source: this.loginSource,
      });
      await this.authSessionStore.refreshSession();
      await this.navigateAfterSuccessfulLogin();
    } catch (error: unknown) {
      const loginStatus = this.getErrorStatus(error);
      if (loginStatus !== 401) {
        this.trackAuthEvent('auth_password_login_failed', {
          source: this.loginSource,
          reason: 'unexpected_status',
        }, {
          status_code: loginStatus ?? 0,
        });
        this.passwordError.set('Could not sign in right now. Please try again.');
        return;
      }

      if (!allowAutoRegistration || this.isAwaitingEmailConfirmationForEmail(email)) {
        this.trackAuthEvent('auth_password_login_requires_confirmation', {
          source: this.loginSource,
        });
        this.passwordError.set('Not confirmed yet. Confirm your email and click "Continue after confirmation".');
        return;
      }

      try {
        await firstValueFrom(this.authApiService.register(email, password));
        this.trackAuthEvent('auth_auto_registration_created', {
          source: this.loginSource,
        });
        this.setAwaitingEmailConfirmation(email);
        this.passwordSuccessMessage.set('Account created. We sent a confirmation link to your email. After confirming, return here and click "Continue after confirmation".');
        this.otpForm.controls.email.setValue(email);
      } catch (registrationError: unknown) {
        const registrationStatus = this.getErrorStatus(registrationError);
        this.trackAuthEvent('auth_auto_registration_failed', {
          source: this.loginSource,
        }, {
          status_code: registrationStatus ?? 0,
        });
        this.passwordError.set(this.buildRegistrationErrorMessage(registrationError));
      }
    } finally {
      this.isBusy.set(false);
    }
  }

  toggleLoginMode(): void {
    const previousMode = this.usePasswordLogin() ? 'password' : 'otp';
    const nextMode = !this.usePasswordLogin();
    this.usePasswordLogin.set(nextMode);
    const newMode = nextMode ? 'password' : 'otp';
    this.trackAuthEvent('auth_login_mode_toggled', {
      source: this.loginSource,
      previous_mode: previousMode,
      new_mode: newMode,
    });

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
    this.trackAuthEvent('auth_otp_request_attempted', {
      source: this.loginSource,
    });

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
      this.trackAuthEvent('auth_otp_requested', {
        source: this.loginSource,
      }, {
        verify_attempts_allowed: otpUiLimits.maxVerifyAttempts,
      });
    } catch {
      this.trackAuthEvent('auth_otp_request_failed', {
        source: this.loginSource,
      });
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
      this.trackAuthEvent('auth_otp_verify_blocked', {
        source: this.loginSource,
        reason: 'code_expired',
      });
      this.otpError.set('This code expired. Request a new one.');
      this.moveToOtpRequestPhase();
      return;
    }

    const email = emailControl.getRawValue().trim();
    const code = codeControl.getRawValue();
    this.trackAuthEvent('auth_otp_verify_attempted', {
      source: this.loginSource,
    });

    this.isBusy.set(true);
    this.otpError.set(null);

    try {
      await firstValueFrom(this.authApiService.verifyOtp(email, code));
      this.trackAuthEvent('auth_login_succeeded', {
        method: 'otp',
        source: this.loginSource,
      });
      await this.authSessionStore.refreshSession();
      await this.navigateAfterSuccessfulLogin();
    } catch (error: unknown) {
      const verifyStatus = this.getErrorStatus(error);
      if (verifyStatus === 401) {
        const attemptsLeft = Math.max(0, this.otpVerifyAttemptsRemaining() - 1);
        this.otpVerifyAttemptsRemaining.set(attemptsLeft);
        this.trackAuthEvent('auth_otp_verify_failed', {
          source: this.loginSource,
          reason: 'invalid_code',
        }, {
          attempts_remaining: attemptsLeft,
          status_code: verifyStatus,
        });
        if (attemptsLeft === 0) {
          this.otpError.set('Too many incorrect attempts. Request a new code.');
          this.moveToOtpRequestPhase();
        } else {
          this.otpError.set(`That code did not work. ${attemptsLeft} attempt(s) left.`);
        }
      } else {
        this.trackAuthEvent('auth_otp_verify_failed', {
          source: this.loginSource,
          reason: 'unexpected_status',
        }, {
          status_code: verifyStatus ?? 0,
        });
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
    if (!this.isBrowser) {
      return;
    }

    this.persistPostLoginReturnUrl(this.postLoginReturnUrl);
    this.persistPostLoginSource(this.loginSource);
    this.trackAuthEvent('auth_external_login_started', {
      provider: providerId,
      source: this.loginSource,
      return_url: this.postLoginReturnUrl,
    });

    const callbackUrl = new URL('/auth/callback', window.location.origin).toString();
    const usersApiUrl = environment.usersApiUrl.replace(/\/$/, '');
    const target = `${usersApiUrl}/users/auth/external/${encodeURIComponent(providerId)}/start?returnUrl=${encodeURIComponent(callbackUrl)}`;
    window.location.assign(target);
  }

  private async navigateAfterSuccessfulLogin(): Promise<void> {
    const target = this.postLoginReturnUrl ?? defaultPostLoginRoute;
    this.trackAuthEvent('auth_login_redirected', {
      source: this.loginSource,
      target,
    });
    this.clearStoredPostLoginReturnUrl();
    this.clearStoredPostLoginSource();
    await this.router.navigateByUrl(target);
  }

  private resolveReturnUrl(value: string | null): string | null {
    if (!value) {
      return null;
    }

    const candidate = value.trim();
    if (!candidate || !candidate.startsWith('/')) {
      return null;
    }

    if (candidate.startsWith('//') || candidate.includes('\r') || candidate.includes('\n')) {
      return null;
    }

    return candidate;
  }

  private persistPostLoginReturnUrl(returnUrl: string | null): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      if (returnUrl) {
        window.sessionStorage.setItem(postLoginReturnUrlStorageKey, returnUrl);
      } else {
        window.sessionStorage.removeItem(postLoginReturnUrlStorageKey);
      }
    } catch {
      // noop
    }
  }

  private clearStoredPostLoginReturnUrl(): void {
    this.persistPostLoginReturnUrl(null);
  }

  private persistPostLoginSource(source: string | null): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      if (source) {
        window.sessionStorage.setItem(postLoginSourceStorageKey, source);
      } else {
        window.sessionStorage.removeItem(postLoginSourceStorageKey);
      }
    } catch {
      // noop
    }
  }

  private clearStoredPostLoginSource(): void {
    this.persistPostLoginSource(null);
  }

  private async loadExternalProviders(): Promise<void> {
    try {
      const providers = await firstValueFrom(this.authApiService.externalProviders());
      this.externalProviders.set(providers);
      this.trackAuthEvent('auth_external_providers_loaded', {
        source: this.loginSource,
      }, {
        provider_count: providers.length,
      });
    } catch {
      this.externalProviders.set([]);
      this.trackAuthEvent('auth_external_providers_load_failed', {
        source: this.loginSource,
      });
    }
  }

  private canRequestOtpForEmail(email: string): boolean {
    const normalizedEmail = this.normalizeEmail(email);
    if (!normalizedEmail) {
      this.trackAuthEvent('auth_otp_request_blocked', {
        source: this.loginSource,
        reason: 'invalid_email',
      });
      this.otpError.set('Enter a valid email.');
      return false;
    }

    if (this.otpCooldownEmail === normalizedEmail && this.otpResendCooldownSeconds() > 0) {
      this.trackAuthEvent('auth_otp_request_blocked', {
        source: this.loginSource,
        reason: 'cooldown_active',
      }, {
        cooldown_seconds_remaining: this.otpResendCooldownSeconds(),
      });
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
      this.trackAuthEvent('auth_otp_request_blocked', {
        source: this.loginSource,
        reason: 'rate_limited',
      }, {
        retry_seconds_remaining: secondsRemaining,
      });
      this.otpError.set(`Too many code requests. Try again in about ${minutesRemaining} minute(s).`);
      return false;
    }

    return true;
  }

  private normalizeEmail(email: string): string | null {
    const normalized = email.trim().toLowerCase();
    return normalized || null;
  }

  private setAwaitingEmailConfirmation(email: string): void {
    const normalizedEmail = this.normalizeEmail(email);
    this.awaitingEmailConfirmation.set(normalizedEmail);
  }

  private isAwaitingEmailConfirmationForEmail(email: string): boolean {
    const normalizedEmail = this.normalizeEmail(email);
    return normalizedEmail !== null && this.awaitingEmailConfirmation() === normalizedEmail;
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

  private resolveSource(value: string | null): string {
    if (!value) {
      return 'direct';
    }

    const candidate = value.trim();
    if (!candidate) {
      return 'direct';
    }

    return candidate.replace(/[^a-zA-Z0-9:_-]/g, '_').slice(0, 80);
  }

  private trackAuthEvent(
    name: string,
    properties: Record<string, string | number | boolean | null | undefined> = {},
    measurements: InsightsMeasurements = {}
  ): void {
    if (!this.insights) {
      return;
    }

    const normalized: Record<string, string> = {};
    for (const [key, value] of Object.entries(properties)) {
      if (value === null || value === undefined) {
        continue;
      }

      normalized[key] = String(value);
    }

    this.insights.trackEvent(name, normalized, measurements);
  }
}

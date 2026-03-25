import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { AuthApiService } from './auth-api.service';
import { AuthSessionState } from '../models/auth-session.model';

const initialState: AuthSessionState = {
  isAuthenticated: false,
  userId: null,
  email: null,
  emailConfirmed: false,
  isLoading: false,
  error: null,
};

@Injectable({ providedIn: 'root' })
export class AuthSessionStore {
  private readonly authApiService = inject(AuthApiService);
  private readonly platformId = inject(PLATFORM_ID);

  private readonly stateSignal = signal<AuthSessionState>(initialState);

  readonly state = this.stateSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.stateSignal().isAuthenticated);
  readonly email = computed(() => this.stateSignal().email);

  async initialize(): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    await this.refreshSession();
  }

  async refreshSession(): Promise<void> {
    this.patchState({ isLoading: true, error: null });

    try {
      const session = await firstValueFrom(this.authApiService.session());
      this.patchState({
        isAuthenticated: session.isAuthenticated,
        userId: session.userId,
        email: session.email,
        emailConfirmed: session.emailConfirmed,
        isLoading: false,
        error: null,
      });
    } catch (error) {
      const response = error as HttpErrorResponse;
      if (response.status === 401 || response.status === 403 || response.status === 302) {
        this.clearSession();
        return;
      }

      this.patchState({
        isLoading: false,
        error: 'Unable to load session right now. Please try again.',
      });
    }
  }

  async logout(): Promise<void> {
    this.patchState({ isLoading: true, error: null });

    try {
      await firstValueFrom(this.authApiService.logout());
      this.clearSession();
    } catch {
      this.patchState({
        isLoading: false,
        error: 'Unable to sign out right now. Please try again.',
      });
    }
  }

  clearError(): void {
    this.patchState({ error: null });
  }

  private clearSession(): void {
    this.patchState({
      isAuthenticated: false,
      userId: null,
      email: null,
      emailConfirmed: false,
      isLoading: false,
      error: null,
    });
  }

  private patchState(patch: Partial<AuthSessionState>): void {
    this.stateSignal.update((current) => ({ ...current, ...patch }));
  }
}

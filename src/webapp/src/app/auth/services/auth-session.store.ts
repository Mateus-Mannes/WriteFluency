import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { AuthApiService } from './auth-api.service';
import { AuthSessionState } from '../models/auth-session.model';

const sessionSnapshotStorageKey = 'wf.auth.session.snapshot.v1';
const sessionRefreshLeadTimeMs = 5 * 60 * 1000;
const minimumScheduledRefreshDelayMs = 15 * 1000;
const fallbackValidationIntervalMs = 15 * 60 * 1000;

const initialState: AuthSessionState = {
  isAuthenticated: false,
  userId: null,
  email: null,
  emailConfirmed: false,
  issuedAtUtc: null,
  expiresAtUtc: null,
  isLoading: false,
  error: null,
};

type AuthSessionSnapshot = Pick<
  AuthSessionState,
  'isAuthenticated' | 'userId' | 'email' | 'emailConfirmed' | 'issuedAtUtc' | 'expiresAtUtc'
> & {
  cachedAtUtc: string;
};

@Injectable({ providedIn: 'root' })
export class AuthSessionStore {
  private readonly authApiService = inject(AuthApiService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  private readonly stateSignal = signal<AuthSessionState>(initialState);
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private validationInterval: ReturnType<typeof setInterval> | null = null;
  private hasRegisteredLifecycleListeners = false;
  private refreshRequestCounter = 0;

  readonly state = this.stateSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.stateSignal().isAuthenticated);
  readonly email = computed(() => this.stateSignal().email);

  async initialize(): Promise<void> {
    if (!this.isBrowser) {
      return;
    }

    this.restoreSnapshot();
    this.startBackgroundValidationLoop();
    this.registerLifecycleListeners();
    this.refreshSessionInBackground();
  }

  async refreshSession(): Promise<void> {
    await this.refreshSessionCore({ showLoading: true, persistErrors: true });
  }

  refreshSessionInBackground(): void {
    if (!this.isBrowser) {
      return;
    }

    void this.refreshSessionCore({ showLoading: false, persistErrors: false });
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

  invalidateSession(): void {
    this.clearSession();
  }

  private async refreshSessionCore(options: { showLoading: boolean; persistErrors: boolean }): Promise<void> {
    const requestId = ++this.refreshRequestCounter;

    if (options.showLoading) {
      this.patchState({ isLoading: true, error: null });
    } else {
      this.patchState({ error: null });
    }

    try {
      const session = await firstValueFrom(this.authApiService.session());
      if (!this.isLatestRefreshRequest(requestId)) {
        return;
      }

      this.patchState({
        isAuthenticated: session.isAuthenticated,
        userId: session.userId,
        email: session.email,
        emailConfirmed: session.emailConfirmed,
        issuedAtUtc: session.issuedAtUtc,
        expiresAtUtc: session.expiresAtUtc,
        isLoading: false,
        error: null,
      });

      this.persistSnapshot();
      this.scheduleRefreshFromState();
    } catch (error) {
      if (!this.isLatestRefreshRequest(requestId)) {
        return;
      }

      const response = error as HttpErrorResponse;
      if (response.status === 401 || response.status === 403 || response.status === 302) {
        this.clearSession();
        return;
      }

      this.scheduleRefreshFallback();

      if (options.persistErrors) {
        this.patchState({
          isLoading: false,
          error: 'Unable to load session right now. Please try again.',
        });
      } else {
        this.patchState({ isLoading: false });
      }
    }
  }

  private isLatestRefreshRequest(requestId: number): boolean {
    return requestId === this.refreshRequestCounter;
  }

  private restoreSnapshot(): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      const rawSnapshot = window.localStorage.getItem(sessionSnapshotStorageKey);
      if (!rawSnapshot) {
        return;
      }

      const snapshot = JSON.parse(rawSnapshot) as Partial<AuthSessionSnapshot>;
      if (typeof snapshot !== 'object' || snapshot === null) {
        return;
      }

      this.patchState({
        isAuthenticated: snapshot.isAuthenticated === true,
        userId: snapshot.userId ?? null,
        email: snapshot.email ?? null,
        emailConfirmed: snapshot.emailConfirmed === true,
        issuedAtUtc: snapshot.issuedAtUtc ?? null,
        expiresAtUtc: snapshot.expiresAtUtc ?? null,
        isLoading: false,
        error: null,
      });

      this.scheduleRefreshFromState();
    } catch {
      window.localStorage.removeItem(sessionSnapshotStorageKey);
    }
  }

  private persistSnapshot(): void {
    if (!this.isBrowser) {
      return;
    }

    const state = this.stateSignal();
    const snapshot: AuthSessionSnapshot = {
      isAuthenticated: state.isAuthenticated,
      userId: state.userId,
      email: state.email,
      emailConfirmed: state.emailConfirmed,
      issuedAtUtc: state.issuedAtUtc,
      expiresAtUtc: state.expiresAtUtc,
      cachedAtUtc: new Date().toISOString(),
    };

    try {
      window.localStorage.setItem(sessionSnapshotStorageKey, JSON.stringify(snapshot));
    } catch {
      // noop
    }
  }

  private scheduleRefreshFromState(): void {
    this.clearRefreshTimer();

    const state = this.stateSignal();
    if (!state.isAuthenticated || !state.expiresAtUtc) {
      this.scheduleRefreshFallback();
      return;
    }

    const expiresAtMs = Date.parse(state.expiresAtUtc);
    if (!Number.isFinite(expiresAtMs)) {
      this.scheduleRefreshFallback();
      return;
    }

    const refreshAtMs = expiresAtMs - sessionRefreshLeadTimeMs;
    const delayMs = Math.max(minimumScheduledRefreshDelayMs, refreshAtMs - Date.now());

    this.refreshTimer = setTimeout(() => {
      this.refreshSessionInBackground();
    }, delayMs);
  }

  private scheduleRefreshFallback(): void {
    this.clearRefreshTimer();
    this.refreshTimer = setTimeout(() => {
      this.refreshSessionInBackground();
    }, fallbackValidationIntervalMs);
  }

  private startBackgroundValidationLoop(): void {
    if (!this.isBrowser || this.validationInterval) {
      return;
    }

    this.validationInterval = setInterval(() => {
      this.refreshSessionInBackground();
    }, fallbackValidationIntervalMs);
  }

  private registerLifecycleListeners(): void {
    if (!this.isBrowser || this.hasRegisteredLifecycleListeners) {
      return;
    }

    this.hasRegisteredLifecycleListeners = true;

    window.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        this.refreshSessionInBackground();
      }
    });

    window.addEventListener('online', () => {
      this.refreshSessionInBackground();
    });
  }

  private clearRefreshTimer(): void {
    if (!this.refreshTimer) {
      return;
    }

    clearTimeout(this.refreshTimer);
    this.refreshTimer = null;
  }

  private clearSession(): void {
    this.clearRefreshTimer();
    this.patchState({
      isAuthenticated: false,
      userId: null,
      email: null,
      emailConfirmed: false,
      issuedAtUtc: null,
      expiresAtUtc: null,
      isLoading: false,
      error: null,
    });
    this.persistSnapshot();
  }

  private patchState(patch: Partial<AuthSessionState>): void {
    this.stateSignal.update((current) => ({ ...current, ...patch }));
  }
}

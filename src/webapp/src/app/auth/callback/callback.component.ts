import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { AuthSessionStore } from '../services/auth-session.store';
import { Insights, InsightsMeasurements } from '../../../telemetry/insights.service';

const postLoginReturnUrlStorageKey = 'wf.auth.post-login-return-url.v1';
const postLoginSourceStorageKey = 'wf.auth.post-login-source.v1';
const defaultPostLoginRoute = '/user';

const callbackErrorMap: Record<string, string> = {
  access_denied: 'Provider access was denied.',
  invalid_state: 'Login state is invalid or expired. Please retry.',
  callback_error: 'Provider callback failed. Please retry.',
  provider_not_enabled: 'Provider is not enabled.',
  provider_email_missing: 'Provider did not return an email address.',
  provider_email_unverified: 'Provider email is not verified.',
  account_locked: 'Your account is locked.',
  linking_denied: 'Existing account must have confirmed email before linking.',
  external_login_conflict: 'This social account is already linked to another user.',
  account_link_failed: 'Unable to link social account.',
  account_provisioning_failed: 'Unable to create account from provider profile.',
  session_refresh_failed: 'Login completed, but we could not refresh your session. Please sign in again.',
};

@Component({
  selector: 'app-callback',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatCardModule],
  templateUrl: './callback.component.html',
  styleUrls: ['./callback.component.scss'],
})
export class CallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authSessionStore = inject(AuthSessionStore);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly insights = inject(Insights, { optional: true });
  private readonly isBrowser = isPlatformBrowser(this.platformId);
  private callbackSource = 'direct';

  readonly isBusy = signal(true);
  readonly isSuccess = signal(false);
  readonly title = signal('Completing Login');
  readonly message = signal('Finalizing external login callback...');

  async ngOnInit(): Promise<void> {
    if (!this.isBrowser) {
      this.isBusy.set(false);
      return;
    }

    const auth = this.route.snapshot.queryParamMap.get('auth');
    const provider = this.route.snapshot.queryParamMap.get('provider');
    const code = this.route.snapshot.queryParamMap.get('code');
    this.callbackSource = this.consumePostLoginSource();
    this.trackAuthEvent('auth_callback_page_viewed', {
      auth,
      provider,
      code,
      source: this.callbackSource,
    });

    if (auth === 'success') {
      await this.handleSuccess(provider);
      return;
    }

    this.handleError(code, provider);
  }

  private async handleSuccess(provider: string | null): Promise<void> {
    this.isSuccess.set(true);
    this.title.set('Login Successful');
    this.message.set(`Logged in${provider ? ` with ${provider}` : ''}. Redirecting...`);
    this.trackAuthEvent('auth_callback_succeeded', {
      provider,
      source: this.callbackSource,
    });

    try {
      await this.authSessionStore.refreshSession();
    } catch {
      this.handleError('session_refresh_failed', provider);
      return;
    }

    this.isBusy.set(false);
    const target = this.consumePostLoginReturnUrl() ?? defaultPostLoginRoute;
    this.trackAuthEvent('auth_login_redirected', {
      source: this.callbackSource,
      target,
    });
    void this.router.navigateByUrl(target);
  }

  private consumePostLoginReturnUrl(): string | null {
    const queryReturnUrl = this.resolveReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
    if (queryReturnUrl) {
      this.clearStoredPostLoginReturnUrl();
      return queryReturnUrl;
    }

    if (!this.isBrowser) {
      return null;
    }

    try {
      const stored = this.resolveReturnUrl(window.sessionStorage.getItem(postLoginReturnUrlStorageKey));
      this.clearStoredPostLoginReturnUrl();
      return stored;
    } catch {
      return null;
    }
  }

  private consumePostLoginSource(): string {
    const querySource = this.resolveSource(this.route.snapshot.queryParamMap.get('source'));
    if (querySource) {
      this.clearStoredPostLoginSource();
      return querySource;
    }

    if (!this.isBrowser) {
      return 'direct';
    }

    try {
      const stored = this.resolveSource(window.sessionStorage.getItem(postLoginSourceStorageKey)) ?? 'direct';
      this.clearStoredPostLoginSource();
      return stored;
    } catch {
      return 'direct';
    }
  }

  private clearStoredPostLoginReturnUrl(): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      window.sessionStorage.removeItem(postLoginReturnUrlStorageKey);
    } catch {
      // noop
    }
  }

  private clearStoredPostLoginSource(): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      window.sessionStorage.removeItem(postLoginSourceStorageKey);
    } catch {
      // noop
    }
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

  private resolveSource(value: string | null): string | null {
    if (!value) {
      return null;
    }

    const candidate = value.trim();
    if (!candidate) {
      return null;
    }

    return candidate.replace(/[^a-zA-Z0-9:_-]/g, '_').slice(0, 80);
  }

  private handleError(code: string | null, provider: string | null): void {
    this.trackAuthEvent('auth_callback_failed', {
      code,
      provider,
      source: this.callbackSource,
    });

    this.isSuccess.set(false);
    this.title.set('Login Failed');

    const friendlyMessage = code ? callbackErrorMap[code] ?? `Authentication failed with code: ${code}` : 'Authentication failed.';
    this.message.set(provider ? `${friendlyMessage} Provider: ${provider}.` : friendlyMessage);

    this.isBusy.set(false);
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

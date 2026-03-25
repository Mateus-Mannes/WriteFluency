import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { AuthSessionStore } from '../services/auth-session.store';

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

  readonly isBusy = signal(true);
  readonly isSuccess = signal(false);
  readonly title = signal('Completing Login');
  readonly message = signal('Finalizing external login callback...');

  async ngOnInit(): Promise<void> {
    const auth = this.route.snapshot.queryParamMap.get('auth');
    const provider = this.route.snapshot.queryParamMap.get('provider');
    const code = this.route.snapshot.queryParamMap.get('code');

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

    await this.authSessionStore.refreshSession();
    this.isBusy.set(false);
    await this.router.navigate(['/']);
  }

  private handleError(code: string | null, provider: string | null): void {
    this.isSuccess.set(false);
    this.title.set('Login Failed');

    const friendlyMessage = code ? callbackErrorMap[code] ?? `Authentication failed with code: ${code}` : 'Authentication failed.';
    this.message.set(provider ? `${friendlyMessage} Provider: ${provider}.` : friendlyMessage);

    this.isBusy.set(false);
  }
}

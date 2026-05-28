import { CommonModule } from '@angular/common';
import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Insights } from '../../telemetry/insights.service';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { BrowserService } from '../core/services/browser.service';
import { BillingApiService } from '../user/services/billing-api.service';

const checkoutLoadingTimeoutMs = 8000;

@Component({
  selector: 'app-plans',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatCardModule, MatProgressSpinnerModule],
  templateUrl: './plans.component.html',
  styleUrls: ['./plans.component.scss'],
})
export class PlansComponent implements OnDestroy {
  private readonly authSessionStore = inject(AuthSessionStore);
  private readonly billingApi = inject(BillingApiService);
  private readonly browser = inject(BrowserService);
  private readonly router = inject(Router);
  private readonly insights = inject(Insights, { optional: true });
  private checkoutLoadingTimer: ReturnType<typeof setTimeout> | null = null;

  readonly authState = this.authSessionStore.state;
  readonly checkoutError = signal<string | null>(null);
  readonly isCheckoutStarting = signal(false);
  private readonly isCheckoutRequestInProgress = signal(false);
  readonly isCheckoutDisabled = computed(() => this.isCheckoutStarting() || this.isCheckoutRequestInProgress());
  readonly planCtaLabel = computed(() => this.authState().isPro ? 'You are already Pro' : 'Subscribe to Pro');

  ngOnDestroy(): void {
    if (this.checkoutLoadingTimer) {
      clearTimeout(this.checkoutLoadingTimer);
      this.checkoutLoadingTimer = null;
    }
  }

  async startCheckout(): Promise<void> {
    if (this.isCheckoutDisabled()) {
      return;
    }

    this.checkoutError.set(null);

    const state = this.authState();
    if (!state.isAuthenticated) {
      await this.router.navigate(['/auth/login'], {
        queryParams: {
          returnUrl: '/plans',
          source: 'plans_checkout',
        },
      });
      return;
    }

    if (state.isPro) {
      await this.router.navigate(['/user']);
      return;
    }

    this.startCheckoutLoadingTimeout();
    this.isCheckoutRequestInProgress.set(true);

    try {
      const response = await firstValueFrom(this.billingApi.createCheckoutSession());
      if (response.checkoutUrl) {
        this.browser.navigateTo(response.checkoutUrl);
        return;
      }

      if (response.status === 'subscription_management_required') {
        await this.authSessionStore.refreshSession();
        await this.router.navigate(['/user']);
        return;
      }

      this.checkoutError.set('Could not start checkout right now. Please try again.');
    } catch (error) {
      this.checkoutError.set('Could not start checkout right now. Please try again.');
      this.insights?.trackException(error, {
        properties: {
          area: 'billing',
          operation: 'plans_start_checkout',
        },
      });
    } finally {
      this.isCheckoutRequestInProgress.set(false);
    }
  }

  private startCheckoutLoadingTimeout(): void {
    if (this.checkoutLoadingTimer) {
      clearTimeout(this.checkoutLoadingTimer);
    }

    this.isCheckoutStarting.set(true);
    this.checkoutLoadingTimer = setTimeout(() => {
      this.isCheckoutStarting.set(false);
      this.checkoutLoadingTimer = null;
    }, checkoutLoadingTimeoutMs);
  }
}

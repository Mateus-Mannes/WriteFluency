import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PropositionsService } from '../../api/listen-and-write/api/propositions.service';
import { Insights } from '../../telemetry/insights.service';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { BrowserService } from '../core/services/browser.service';
import { ProCtaTelemetryService } from '../core/services/pro-cta-telemetry.service';
import { BillingApiService } from '../user/services/billing-api.service';

const checkoutLoadingTimeoutMs = 8000;
const catalogExerciseCountPlaceholder = 2900;
const catalogCountAnimationDurationMs = 700;

@Component({
  selector: 'app-plans',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatCardModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './plans.component.html',
  styleUrls: ['./plans.component.scss'],
})
export class PlansComponent implements OnDestroy, OnInit {
  private readonly authSessionStore = inject(AuthSessionStore);
  private readonly billingApi = inject(BillingApiService);
  private readonly propositionsService = inject(PropositionsService);
  private readonly browser = inject(BrowserService);
  private readonly router = inject(Router);
  private readonly proCtaTelemetry = inject(ProCtaTelemetryService);
  private readonly insights = inject(Insights, { optional: true });
  private checkoutLoadingTimer: ReturnType<typeof setTimeout> | null = null;
  private catalogCountAnimationFrameId: number | null = null;

  readonly authState = this.authSessionStore.state;
  readonly catalogExerciseCount = signal(catalogExerciseCountPlaceholder);
  readonly checkoutError = signal<string | null>(null);
  readonly isCheckoutStarting = signal(false);
  private readonly isCheckoutRequestInProgress = signal(false);
  readonly isCheckoutDisabled = computed(() => this.isCheckoutStarting() || this.isCheckoutRequestInProgress());
  readonly isPortalEligible = computed(() => this.authState().entitlementStatus === 'pro_active'
    || this.authState().entitlementStatus === 'pro_canceling');
  readonly planCtaLabel = computed(() => this.isPortalEligible() ? 'Manage subscription' : 'Subscribe to Pro');
  readonly planStatusMessage = computed(() => {
    const state = this.authState();
    if (state.entitlementStatus === 'pro_canceling' && state.currentPeriodEndUtc) {
      return `You are already Pro. Access remains active until ${this.formatDate(state.currentPeriodEndUtc)}.`;
    }

    if (state.entitlementStatus === 'pro_active') {
      return 'You are already Pro. Manage your subscription anytime.';
    }

    return null;
  });
  readonly catalogCountLabel = computed(() => {
    return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(this.catalogExerciseCount());
  });
  readonly proCatalogFeatureLabel = computed(() => {
    return `${this.catalogCountLabel()} exercises and growing`;
  });
  readonly proCatalogMeterLabel = computed(() => {
    return `${this.catalogCountLabel()} with Pro`;
  });

  ngOnInit(): void {
    this.loadCatalogExerciseCount();
  }

  ngOnDestroy(): void {
    if (this.checkoutLoadingTimer) {
      clearTimeout(this.checkoutLoadingTimer);
      this.checkoutLoadingTimer = null;
    }

    if (this.catalogCountAnimationFrameId !== null && this.browser.isBrowserEnvironment()) {
      window.cancelAnimationFrame(this.catalogCountAnimationFrameId);
      this.catalogCountAnimationFrameId = null;
    }
  }

  async startCheckout(): Promise<void> {
    if (this.isCheckoutDisabled()) {
      return;
    }

    this.checkoutError.set(null);

    const state = this.authState();
    this.proCtaTelemetry.trackClicked({
      ctaId: this.isPortalEligible() ? 'plans_manage_subscription' : 'plans_subscribe_to_pro',
      location: 'plans_pro_card',
      label: this.planCtaLabel(),
      destination: !state.isAuthenticated
        ? '/auth/login'
        : this.isPortalEligible()
          ? 'stripe_billing_portal'
          : 'stripe_checkout',
      source: 'plans_checkout',
    });

    if (!state.isAuthenticated) {
      await this.router.navigate(['/auth/login'], {
        queryParams: {
          returnUrl: '/plans',
          source: 'plans_checkout',
        },
      });
      return;
    }

    if (this.isPortalEligible()) {
      await this.startPortalSession();
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

  private async startPortalSession(): Promise<void> {
    this.startCheckoutLoadingTimeout();
    this.isCheckoutRequestInProgress.set(true);

    try {
      const response = await firstValueFrom(this.billingApi.createPortalSession());
      if (response.portalUrl) {
        this.browser.navigateTo(response.portalUrl);
        return;
      }

      this.checkoutError.set('Could not open subscription management right now. Please try again.');
    } catch (error) {
      this.checkoutError.set('Could not open subscription management right now. Please try again.');
      this.insights?.trackException(error, {
        properties: {
          area: 'billing',
          operation: 'plans_manage_subscription',
        },
      });
    } finally {
      this.isCheckoutRequestInProgress.set(false);
    }
  }

  private formatDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return 'the end of your billing period';
    }

    return new Intl.DateTimeFormat(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    }).format(date);
  }

  private loadCatalogExerciseCount(): void {
    this.propositionsService.apiPropositionExercisesGet(
      undefined,
      undefined,
      1,
      1,
      'newest',
      undefined
    ).subscribe({
      next: response => {
        if (typeof response.totalCount === 'number' && response.totalCount > 0) {
          this.animateCatalogExerciseCount(response.totalCount);
        }
      },
      error: error => {
        this.insights?.trackException(error, {
          properties: {
            area: 'plans',
            operation: 'plans_load_catalog_count',
          },
        });
      },
    });
  }

  private animateCatalogExerciseCount(targetCount: number): void {
    const startCount = this.catalogExerciseCount();
    if (startCount === targetCount) {
      return;
    }

    if (!this.browser.isBrowserEnvironment() || typeof window.requestAnimationFrame !== 'function') {
      this.catalogExerciseCount.set(targetCount);
      return;
    }

    if (this.catalogCountAnimationFrameId !== null) {
      window.cancelAnimationFrame(this.catalogCountAnimationFrameId);
    }

    const startedAt = Date.now();
    const step = (): void => {
      const elapsed = Date.now() - startedAt;
      const progress = Math.min(1, elapsed / catalogCountAnimationDurationMs);
      const easedProgress = 1 - Math.pow(1 - progress, 3);
      const nextCount = Math.round(startCount + ((targetCount - startCount) * easedProgress));

      this.catalogExerciseCount.set(nextCount);

      if (progress < 1) {
        this.catalogCountAnimationFrameId = window.requestAnimationFrame(step);
        return;
      }

      this.catalogExerciseCount.set(targetCount);
      this.catalogCountAnimationFrameId = null;
    };

    this.catalogCountAnimationFrameId = window.requestAnimationFrame(step);
  }
}

import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FeedbackPromptStatusResponse } from '../auth/models/feedback-prompt.model';
import { AuthEntitlementStatus } from '../auth/models/auth-session.model';
import { AuthApiService } from '../auth/services/auth-api.service';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { progressFeedbackVideoConfig } from '../core/config/progress-feedback-video.config';
import { BrowserService } from '../core/services/browser.service';
import { ProCtaTelemetryService } from '../core/services/pro-cta-telemetry.service';
import {
  ProgressFeedbackDismissReason,
  ProgressFeedbackModalComponent,
} from '../shared/progress-feedback-modal/progress-feedback-modal.component';
import { ProgressItemResponse, ProgressSummaryResponse } from './models/user-progress.model';
import { BillingApiService } from './services/billing-api.service';
import { UserProgressApiService } from './services/user-progress-api.service';
import { Insights } from '../../telemetry/insights.service';

const progressFeedbackCampaignKey = 'progress_feedback_v1';
const progressFeedbackMinimumCompletedExercises = 3;
const progressFeedbackCommentMaxLength = 4000;
const subscriptionManagementLoadingTimeoutMs = 8000;

@Component({
  selector: 'app-user',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    ProgressFeedbackModalComponent,
  ],
  templateUrl: './user.component.html',
  styleUrls: ['./user.component.scss'],
})
export class UserComponent implements OnInit, OnDestroy {
  private readonly authSessionStore = inject(AuthSessionStore);
  private readonly authApi = inject(AuthApiService);
  private readonly billingApi = inject(BillingApiService);
  private readonly browser = inject(BrowserService);
  private readonly proCtaTelemetry = inject(ProCtaTelemetryService);
  private readonly userProgressApi = inject(UserProgressApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly insights = inject(Insights, { optional: true });
  private subscriptionManagementLoadingTimer: ReturnType<typeof setTimeout> | null = null;

  readonly authState = this.authSessionStore.state;
  readonly progressFeedbackVideoConfig = progressFeedbackVideoConfig;

  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly summary = signal<ProgressSummaryResponse | null>(null);
  readonly items = signal<ProgressItemResponse[]>([]);
  readonly isProgressFeedbackModalOpen = signal(false);
  readonly billingMessage = signal<string | null>(null);
  readonly billingError = signal<string | null>(null);
  readonly isBillingActionInProgress = signal(false);
  readonly isSubscriptionManagementLoading = signal(false);
  private readonly isSubscriptionManagementRequestInProgress = signal(false);
  readonly hasItems = computed(() => this.items().length > 0);
  readonly canSubscribeToPro = computed(() => {
    const state = this.authState();
    return state.isAuthenticated && !state.isPro;
  });
  readonly canManageSubscription = computed(() => {
    const status = this.authState().entitlementStatus;
    return status === 'pro_active' || status === 'pro_canceling';
  });
  readonly isSubscriptionManagementDisabled = computed(() =>
    this.isSubscriptionManagementLoading() || this.isSubscriptionManagementRequestInProgress());

  async ngOnInit(): Promise<void> {
    await this.handleBillingReturn();
    await this.reload();
  }

  ngOnDestroy(): void {
    if (this.subscriptionManagementLoadingTimer) {
      clearTimeout(this.subscriptionManagementLoadingTimer);
      this.subscriptionManagementLoadingTimer = null;
    }
  }

  async reload(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      const [summary, items] = await Promise.all([
        firstValueFrom(this.userProgressApi.summary()),
        firstValueFrom(this.userProgressApi.items()),
      ]);

      this.summary.set(summary);
      this.items.set(items);
      void this.evaluateProgressFeedbackPrompt(summary);
    } catch (error) {
      this.summary.set(null);
      this.items.set([]);
      const statusCode = this.getStatusCode(error);
      const errorKind = this.getErrorKind(error, statusCode);

      if (statusCode === 401) {
        const hadAuthenticatedSession = this.authState().isAuthenticated;
        const unauthorizedReason = hadAuthenticatedSession ? 'session_expired' : 'missing_session';
        const redirectSource = hadAuthenticatedSession
          ? 'user_progress_session_expired'
          : 'user_progress_unauthorized';

        if (hadAuthenticatedSession) {
          this.authSessionStore.invalidateSession();
        }

        this.insights?.trackEvent(
          redirectSource,
          {
            area: 'user_progress',
            operation: 'load_user_progress',
            error_kind: unauthorizedReason,
            http_status: String(statusCode),
          },
          {
            http_status: statusCode,
          },
        );

        const redirected = await this.router.navigate(['/auth/login'], {
          queryParams: {
            returnUrl: '/user',
            source: redirectSource,
          },
        });

        if (!redirected) {
          this.error.set('Your session expired. Please log in again.');
        }

        return;
      }

      this.error.set('Could not load progress right now. Please try again.');
      this.insights?.trackException(error, {
        properties: {
          area: 'user_progress',
          operation: 'load_user_progress',
          error_kind: errorKind,
          http_status: statusCode === null ? 'unknown' : String(statusCode),
        },
        measurements: {
          http_status: statusCode ?? 0,
        },
      });
    } finally {
      this.isLoading.set(false);
    }
  }

  onProgressFeedbackDismissed(reason: ProgressFeedbackDismissReason): void {
    if (!this.isProgressFeedbackModalOpen()) {
      return;
    }

    this.isProgressFeedbackModalOpen.set(false);
    this.insights?.trackEvent(
      'progress_feedback_modal_dismissed',
      {
        ...this.buildProgressFeedbackTelemetryProperties(),
        dismiss_reason: reason,
      },
      this.buildProgressFeedbackTelemetryMeasurements(),
    );

    void firstValueFrom(this.authApi.markFeedbackPromptDismissed(progressFeedbackCampaignKey)).catch((error) => {
      this.trackProgressFeedbackPromptException(error, 'mark_progress_feedback_prompt_dismissed');
    });
  }

  onProgressFeedbackSubmitted(comment: string): void {
    const trimmedComment = comment.trim();
    if (!trimmedComment) {
      return;
    }

    const normalizedComment = trimmedComment.slice(0, progressFeedbackCommentMaxLength);
    this.isProgressFeedbackModalOpen.set(false);
    this.insights?.trackEvent(
      'progress_feedback_submitted',
      {
        ...this.buildProgressFeedbackTelemetryProperties(),
        comment: normalizedComment,
      },
      {
        ...this.buildProgressFeedbackTelemetryMeasurements(),
        comment_length: normalizedComment.length,
      },
    );

    void firstValueFrom(this.authApi.markFeedbackPromptSubmitted(progressFeedbackCampaignKey)).catch((error) => {
      this.trackProgressFeedbackPromptException(error, 'mark_progress_feedback_prompt_submitted');
    });
  }

  async startSubscriptionManagement(): Promise<void> {
    if (this.isSubscriptionManagementDisabled() || !this.canManageSubscription()) {
      return;
    }

    this.proCtaTelemetry.trackClicked({
      ctaId: 'progress_manage_subscription',
      location: 'user_progress_account_card',
      label: 'Manage my subscription',
      destination: 'stripe_billing_portal',
    });
    this.startSubscriptionManagementLoadingTimeout();
    this.isSubscriptionManagementRequestInProgress.set(true);
    this.billingMessage.set(null);
    this.billingError.set(null);

    try {
      const response = await firstValueFrom(this.billingApi.createPortalSession());
      if (response.portalUrl) {
        this.browser.navigateTo(response.portalUrl);
        return;
      }

      this.billingError.set('Could not open subscription management right now. Please try again.');
    } catch (error) {
      this.billingError.set('Could not open subscription management right now. Please try again.');
      this.trackBillingException(error, 'create_portal_session');
    } finally {
      this.isSubscriptionManagementRequestInProgress.set(false);
    }
  }

  private startSubscriptionManagementLoadingTimeout(): void {
    if (this.subscriptionManagementLoadingTimer) {
      clearTimeout(this.subscriptionManagementLoadingTimer);
    }

    this.isSubscriptionManagementLoading.set(true);
    this.subscriptionManagementLoadingTimer = setTimeout(() => {
      this.isSubscriptionManagementLoading.set(false);
      this.subscriptionManagementLoadingTimer = null;
    }, subscriptionManagementLoadingTimeoutMs);
  }

  private async handleBillingReturn(): Promise<void> {
    const checkoutState = this.route.snapshot.queryParamMap.get('checkout');
    if (checkoutState === 'cancelled') {
      this.billingMessage.set('Checkout was cancelled.');
      await this.clearBillingQueryParams();
      return;
    }

    if (checkoutState === 'success') {
      await this.handleCheckoutSuccessReturn();
      return;
    }

    const billingState = this.route.snapshot.queryParamMap.get('billing');
    if (billingState === 'returned') {
      await this.handlePortalReturn();
    }
  }

  private async handleCheckoutSuccessReturn(): Promise<void> {
    const sessionId = this.route.snapshot.queryParamMap.get('session_id');
    if (!sessionId) {
      this.billingError.set('Checkout returned without a session ID.');
      await this.clearBillingQueryParams();
      return;
    }

    this.isBillingActionInProgress.set(true);
    this.billingMessage.set(null);
    this.billingError.set(null);

    try {
      await firstValueFrom(this.billingApi.confirmCheckoutSession(sessionId));
      await this.authSessionStore.refreshSession();
      this.billingMessage.set('Your Pro subscription is active.');
      this.insights?.trackEvent('checkout_confirmed', {
        area: 'billing',
        checkout_session_id: sessionId,
      });
    } catch (error) {
      this.billingError.set('Could not confirm checkout. Please try again.');
      this.trackBillingException(error, 'confirm_checkout');
    } finally {
      this.isBillingActionInProgress.set(false);
      await this.clearBillingQueryParams();
    }
  }

  private async handlePortalReturn(): Promise<void> {
    this.isBillingActionInProgress.set(true);
    this.billingMessage.set(null);
    this.billingError.set(null);

    try {
      await firstValueFrom(this.billingApi.syncSubscription());
      await this.authSessionStore.refreshSession();
      this.billingMessage.set('Subscription details updated.');
      this.insights?.trackEvent('subscription_synced', {
        area: 'billing',
        source: 'portal_return',
      });
    } catch (error) {
      this.billingError.set('Could not refresh subscription status. Please try again.');
      this.trackBillingException(error, 'sync_subscription');
    } finally {
      this.isBillingActionInProgress.set(false);
      await this.clearBillingQueryParams();
    }
  }

  private async clearBillingQueryParams(): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        checkout: null,
        session_id: null,
        billing: null,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  onSubscribeToProClick(source: 'account_card' | 'billing_error'): void {
    this.proCtaTelemetry.trackClicked({
      ctaId: source === 'account_card'
        ? 'progress_subscribe_to_pro'
        : 'progress_billing_error_view_plans',
      location: `user_progress_${source}`,
      label: source === 'account_card' ? 'Subscribe to Pro' : 'View plans',
      destination: '/plans',
      source: source === 'account_card' ? 'progress_account_card' : 'progress_billing_error',
    });
  }

  private async evaluateProgressFeedbackPrompt(summary: ProgressSummaryResponse): Promise<void> {
    if (summary.completedCount < progressFeedbackMinimumCompletedExercises) {
      return;
    }

    try {
      const status = await firstValueFrom(this.authApi.feedbackPromptStatus(progressFeedbackCampaignKey));
      if (!status.isEligible) {
        return;
      }

      await firstValueFrom(this.authApi.markFeedbackPromptShown(progressFeedbackCampaignKey));
      this.isProgressFeedbackModalOpen.set(true);
      this.insights?.trackEvent(
        'progress_feedback_modal_opened',
        this.buildProgressFeedbackTelemetryProperties(status),
        this.buildProgressFeedbackTelemetryMeasurements(summary),
      );
    } catch (error) {
      this.trackProgressFeedbackPromptException(error, 'load_progress_feedback_prompt');
    }
  }

  private buildProgressFeedbackTelemetryProperties(
    status?: FeedbackPromptStatusResponse,
  ): Record<string, string> {
    return {
      area: 'user_progress',
      campaign_key: progressFeedbackCampaignKey,
      user_id: this.authState().userId ?? '',
      prompt_is_eligible: status === undefined ? '' : String(status.isEligible),
      prompt_next_eligible_at_utc: status?.nextEligibleAtUtc ?? '',
      progress_last_activity_at_utc: this.summary()?.lastActivityAtUtc ?? '',
    };
  }

  private trackBillingException(error: unknown, operation: string): void {
    const statusCode = this.getStatusCode(error);
    this.insights?.trackException(error, {
      properties: {
        area: 'billing',
        operation,
        error_kind: this.getErrorKind(error, statusCode),
        http_status: statusCode === null ? 'unknown' : String(statusCode),
      },
      measurements: {
        http_status: statusCode ?? 0,
      },
    });
  }

  private buildProgressFeedbackTelemetryMeasurements(
    summary = this.summary(),
  ): Record<string, number> {
    return {
      completed_count: summary?.completedCount ?? 0,
      total_attempts: summary?.totalAttempts ?? 0,
      average_accuracy_percentage: summary?.averageAccuracyPercentage ?? 0,
    };
  }

  private trackProgressFeedbackPromptException(error: unknown, operation: string): void {
    this.insights?.trackException(error, {
      properties: {
        area: 'user_progress',
        operation,
        campaign_key: progressFeedbackCampaignKey,
        user_id: this.authState().userId ?? '',
      },
    });
  }

  private getStatusCode(error: unknown): number | null {
    if (error instanceof HttpErrorResponse && Number.isFinite(error.status)) {
      return error.status;
    }

    return null;
  }

  private getErrorKind(error: unknown, statusCode: number | null): string {
    if (statusCode === 401) {
      return 'unauthorized';
    }

    if (error instanceof Error && error.name === 'TimeoutError') {
      return 'timeout';
    }

    return 'request_failure';
  }

  formatPercent(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }

    return `${(value * 100).toFixed(1)}%`;
  }

  formatDuration(totalSeconds: number | null | undefined): string {
    if (totalSeconds === null || totalSeconds === undefined || Number.isNaN(totalSeconds)) {
      return '0:00';
    }

    const normalizedSeconds = Math.max(0, Math.floor(totalSeconds));
    const hours = Math.floor(normalizedSeconds / 3600);
    const minutes = Math.floor((normalizedSeconds % 3600) / 60);
    const seconds = normalizedSeconds % 60;

    if (hours > 0) {
      return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
    }

    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  }

  formatWordProgress(currentWordCount: number | null | undefined, originalWordCount: number | null | undefined): string {
    const written = currentWordCount === null || currentWordCount === undefined || Number.isNaN(currentWordCount)
      ? 0
      : Math.max(0, Math.floor(currentWordCount));

    const total = originalWordCount === null || originalWordCount === undefined || Number.isNaN(originalWordCount)
      ? '—'
      : String(Math.max(0, Math.floor(originalWordCount)));

    return `${written}/${total}`;
  }

  statusLabel(status: string): string {
    if (status === 'completed') {
      return 'Completed';
    }

    if (status === 'in_progress') {
      return 'In progress';
    }

    return 'Unavailable';
  }

  planStatusLabel(status: AuthEntitlementStatus): string {
    if (status === 'pro_active') {
      return 'Pro active';
    }

    if (status === 'pro_canceling') {
      return 'Pro canceling';
    }

    if (status === 'pro_expired') {
      return 'Pro expired';
    }

    return 'Free';
  }

  formatBillingDate(value: string | null | undefined): string {
    if (!value) {
      return 'the end of your billing period';
    }

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

  trackByExerciseId(_index: number, item: ProgressItemResponse): number {
    return item.exerciseId;
  }

  canOpenExercise(item: ProgressItemResponse): boolean {
    return Number.isFinite(item.exerciseId) && item.exerciseId > 0;
  }

  exerciseLink(item: ProgressItemResponse): (string | number)[] {
    return ['/english-writing-exercise', item.exerciseId];
  }
}

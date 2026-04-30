import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FeedbackPromptStatusResponse } from '../auth/models/feedback-prompt.model';
import { AuthApiService } from '../auth/services/auth-api.service';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { progressFeedbackVideoConfig } from '../core/config/progress-feedback-video.config';
import {
  ProgressFeedbackDismissReason,
  ProgressFeedbackModalComponent,
} from '../shared/progress-feedback-modal/progress-feedback-modal.component';
import { ProgressItemResponse, ProgressSummaryResponse } from './models/user-progress.model';
import { UserProgressApiService } from './services/user-progress-api.service';
import { Insights } from '../../telemetry/insights.service';

const progressFeedbackCampaignKey = 'progress_feedback_v1';
const progressFeedbackMinimumCompletedExercises = 3;
const progressFeedbackCommentMaxLength = 4000;

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
export class UserComponent implements OnInit {
  private readonly authSessionStore = inject(AuthSessionStore);
  private readonly authApi = inject(AuthApiService);
  private readonly userProgressApi = inject(UserProgressApiService);
  private readonly router = inject(Router);
  private readonly insights = inject(Insights, { optional: true });

  readonly authState = this.authSessionStore.state;
  readonly progressFeedbackVideoConfig = progressFeedbackVideoConfig;

  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly summary = signal<ProgressSummaryResponse | null>(null);
  readonly items = signal<ProgressItemResponse[]>([]);
  readonly isProgressFeedbackModalOpen = signal(false);
  readonly hasItems = computed(() => this.items().length > 0);

  async ngOnInit(): Promise<void> {
    await this.reload();
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

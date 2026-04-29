import { Injectable, Optional, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { EMPTY } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Proposition } from 'src/api/listen-and-write/model/proposition';
import { TextComparisonResult } from 'src/api/listen-and-write';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { UserProgressApiService } from '../../user/services/user-progress-api.service';
import { ProgressStateResponse, SaveProgressStateRequest } from '../../user/models/user-progress.model';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { Insights } from '../../../telemetry/insights.service';

export type PersistedExerciseState = 'intro' | 'exercise' | 'results';

export interface ExerciseProgressStateSnapshot {
  exerciseState: PersistedExerciseState;
  userText: string | null;
  autoPauseSeconds: number | null;
  pausedTimeSeconds: number | null;
}

type ProgressSyncNotificationKind = 'warning' | 'session_expired';
type ProgressFailureKind = 'unauthorized' | 'api_error';

export interface ProgressSyncNotification {
  id: number;
  kind: ProgressSyncNotificationKind;
  message: string;
}

type ProgressOperation = 'start' | 'save_state' | 'complete' | 'load_state';

const stateSaveDebounceMs = 2000;
const notificationAutoDismissMs = 10000;

@Injectable({ providedIn: 'root' })
export class ExerciseProgressTrackingService {
  private pendingStateSaveTimer: ReturnType<typeof setTimeout> | null = null;
  private pendingStateSaveRequest: SaveProgressStateRequest | null = null;
  private notificationDismissTimer: ReturnType<typeof setTimeout> | null = null;

  private notificationCounter = 0;
  private hasShownApiFailureNotificationForCurrentTry = false;
  private hasShownSessionExpiredNotificationForCurrentTry = false;
  private currentTryExerciseId: number | null = null;
  private readonly syncNotificationSignal = signal<ProgressSyncNotification | null>(null);

  constructor(
    private readonly authSessionStore: AuthSessionStore,
    private readonly userProgressApi: UserProgressApiService,
    private readonly exerciseSessionTracking: ExerciseSessionTrackingService,
    @Optional() private readonly insights: Insights | null = null,
  ) {}

  readonly syncNotification = this.syncNotificationSignal.asReadonly();

  trackStart(proposition: Proposition | null): void {
    const exerciseId = proposition?.id;
    if (!this.authSessionStore.isAuthenticated() || exerciseId === undefined) {
      return;
    }

    this.markTryStarted(exerciseId);
    const metadata = this.buildExerciseMetadata(proposition);

    this.userProgressApi.start({
      exerciseId,
      ...metadata,
      originalWordCount: this.countWords(proposition?.text),
    })
      .pipe(catchError((error) => this.handleProgressApiError('start', exerciseId, error)))
      .subscribe();
  }

  trackComplete(proposition: Proposition | null, result: TextComparisonResult | null): void {
    const exerciseId = proposition?.id;
    if (!this.authSessionStore.isAuthenticated() || exerciseId === undefined) {
      return;
    }

    this.ensureTryContext(exerciseId);
    this.clearPendingStateSave();
    const metadata = this.buildExerciseMetadata(proposition);

    this.userProgressApi.complete({
      exerciseId,
      accuracyPercentage: result?.accuracyPercentage ?? null,
      wordCount: this.countWords(result?.userText),
      originalWordCount: this.resolveOriginalWordCount(proposition, result),
      ...metadata,
    })
      .pipe(catchError((error) => this.handleProgressApiError('complete', exerciseId, error)))
      .subscribe();
  }

  saveState(proposition: Proposition | null, snapshot: ExerciseProgressStateSnapshot): void {
    const exerciseId = proposition?.id;
    if (!this.authSessionStore.isAuthenticated() || exerciseId === undefined) {
      return;
    }

    this.ensureTryContext(exerciseId);
    const metadata = this.buildExerciseMetadata(proposition);

    this.pendingStateSaveRequest = {
      exerciseId,
      exerciseState: snapshot.exerciseState,
      userText: snapshot.userText ?? null,
      wordCount: this.countWords(snapshot.userText),
      originalWordCount: this.countWords(proposition?.text),
      autoPauseSeconds: this.normalizeAutoPause(snapshot.autoPauseSeconds),
      pausedTimeSeconds: this.normalizePausedTime(snapshot.pausedTimeSeconds),
      ...metadata,
    };

    if (this.pendingStateSaveTimer) {
      clearTimeout(this.pendingStateSaveTimer);
    }

    this.pendingStateSaveTimer = setTimeout(() => {
      const request = this.pendingStateSaveRequest;
      this.pendingStateSaveTimer = null;
      this.pendingStateSaveRequest = null;

      if (!request) {
        return;
      }

      if (!this.authSessionStore.isAuthenticated()) {
        return;
      }

      this.userProgressApi.saveState(request)
        .pipe(catchError((error) => this.handleProgressApiError('save_state', request.exerciseId, error)))
        .subscribe();
    }, stateSaveDebounceMs);
  }

  async loadState(exerciseId: number): Promise<ProgressStateResponse | null> {
    if (!this.authSessionStore.isAuthenticated() || !Number.isFinite(exerciseId) || exerciseId <= 0) {
      return null;
    }

    this.ensureTryContext(exerciseId);

    try {
      const response = await firstValueFrom(this.userProgressApi.state(exerciseId));
      if (!response.trackingEnabled || !response.hasServerState) {
        return null;
      }

      return response;
    } catch (error) {
      this.handleProgressApiError('load_state', exerciseId, error);
      return null;
    }
  }

  dismissSyncNotification(): void {
    this.clearNotificationDismissTimer();
    this.syncNotificationSignal.set(null);
  }

  private handleProgressApiError(operation: ProgressOperation, exerciseId: number, error: unknown) {
    const statusCode = this.extractStatusCode(error);

    if (statusCode === 401) {
      const shouldShowSessionExpiredNotification =
        this.authSessionStore.isAuthenticated()
        && !this.authSessionStore.isLogoutInProgress();

      this.clearPendingStateSave();
      this.authSessionStore.invalidateSession();

      if (shouldShowSessionExpiredNotification && !this.hasShownSessionExpiredNotificationForCurrentTry) {
        this.hasShownSessionExpiredNotificationForCurrentTry = true;
        const notification = {
          kind: 'session_expired',
          message: 'Your session expired. Please log in again to keep saving your exercise progress.',
        } as const;
        this.showNotification(notification);
        this.trackProgressSyncNotification(operation, exerciseId, statusCode, 'unauthorized', notification, error);
      }

      this.trackProgressSyncFailure(operation, exerciseId, statusCode, 'unauthorized');
      return EMPTY;
    }

    if (!this.hasShownApiFailureNotificationForCurrentTry) {
      this.hasShownApiFailureNotificationForCurrentTry = true;
      const notification = {
        kind: 'warning',
        message: 'We had a problem saving your progress. You can continue the exercise, but your latest state may not be fully saved.',
      } as const;
      this.showNotification(notification);
      this.trackProgressSyncNotification(operation, exerciseId, statusCode, 'api_error', notification, error);
    }

    this.trackProgressSyncFailure(operation, exerciseId, statusCode, 'api_error');
    return EMPTY;
  }

  private trackProgressSyncFailure(
    operation: ProgressOperation,
    exerciseId: number,
    statusCode: number | null,
    failureKind: ProgressFailureKind,
  ): void {
    const properties = {
      operation,
      failure_kind: failureKind,
      http_status: statusCode === null ? 'unknown' : String(statusCode),
      exercise_id: String(exerciseId),
    };
    const measurements = {
      http_status: statusCode ?? 0,
    };

    if (this.exerciseSessionTracking.hasActiveSession()) {
      this.exerciseSessionTracking.trackEvent('exercise_progress_sync_failure', properties, measurements);
      return;
    }

    this.insights?.trackEvent('exercise_progress_sync_failure', properties, measurements);
  }

  private trackProgressSyncNotification(
    operation: ProgressOperation,
    exerciseId: number,
    statusCode: number | null,
    failureKind: ProgressFailureKind,
    notification: Omit<ProgressSyncNotification, 'id'>,
    error: unknown,
  ): void {
    const sessionId = this.exerciseSessionTracking.getCurrentSessionId();
    const operationId = this.exerciseSessionTracking.getCurrentOperationId();
    const properties = {
      operation,
      failure_kind: failureKind,
      notification_kind: notification.kind,
      notification_message: notification.message,
      http_status: statusCode === null ? 'unknown' : String(statusCode),
      exercise_id: String(exerciseId),
      wf_session_id: sessionId ?? '',
      wf_operation_id: operationId ?? '',
      error_name: error instanceof Error ? error.name : '',
    };
    const measurements = {
      http_status: statusCode ?? 0,
    };

    this.insights?.trackEvent('exercise_progress_sync_notification', properties, measurements);
  }

  private showNotification(notification: Omit<ProgressSyncNotification, 'id'>): void {
    this.clearNotificationDismissTimer();

    this.notificationCounter += 1;
    this.syncNotificationSignal.set({
      id: this.notificationCounter,
      ...notification,
    });

    this.notificationDismissTimer = setTimeout(() => {
      this.syncNotificationSignal.set(null);
      this.notificationDismissTimer = null;
    }, notificationAutoDismissMs);
  }

  private clearNotificationDismissTimer(): void {
    if (!this.notificationDismissTimer) {
      return;
    }

    clearTimeout(this.notificationDismissTimer);
    this.notificationDismissTimer = null;
  }

  private extractStatusCode(error: unknown): number | null {
    if (error instanceof HttpErrorResponse) {
      return Number.isFinite(error.status) ? error.status : null;
    }

    if (typeof error === 'object' && error !== null && 'status' in error) {
      const status = (error as { status?: unknown }).status;
      if (typeof status === 'number' && Number.isFinite(status)) {
        return status;
      }
    }

    return null;
  }

  private markTryStarted(exerciseId: number): void {
    this.currentTryExerciseId = exerciseId;
    this.hasShownApiFailureNotificationForCurrentTry = false;
    this.hasShownSessionExpiredNotificationForCurrentTry = false;
  }

  private ensureTryContext(exerciseId: number): void {
    if (this.currentTryExerciseId === exerciseId) {
      return;
    }

    this.markTryStarted(exerciseId);
  }

  private countWords(text: string | null | undefined): number {
    return (text || '').trim().split(/\s+/).filter(Boolean).length;
  }

  private resolveOriginalWordCount(
    proposition: Proposition | null,
    result: TextComparisonResult | null,
  ): number {
    const resultWordCount = this.countWords(result?.originalText);
    if (resultWordCount > 0) {
      return resultWordCount;
    }

    return this.countWords(proposition?.text);
  }

  private buildExerciseMetadata(proposition: Proposition | null): {
    exerciseTitle: string | null;
    subject: string | null;
    complexity: string | null;
  } {
    return {
      exerciseTitle: this.normalizeMetadataValue(proposition?.title),
      subject: this.normalizeMetadataValue(proposition?.subject?.description)
        ?? this.normalizeMetadataValue(proposition?.subjectId),
      complexity: this.normalizeMetadataValue(proposition?.complexity?.description)
        ?? this.normalizeMetadataValue(proposition?.complexityId),
    };
  }

  private normalizeMetadataValue(value: string | null | undefined): string | null {
    if (!value) {
      return null;
    }

    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  private normalizeAutoPause(value: number | null | undefined): number | null {
    if (value === null || value === undefined || !Number.isFinite(value)) {
      return null;
    }

    return Math.max(0, Math.round(value));
  }

  private normalizePausedTime(value: number | null | undefined): number | null {
    if (value === null || value === undefined || !Number.isFinite(value)) {
      return null;
    }

    return Math.max(0, value);
  }

  private clearPendingStateSave(): void {
    if (this.pendingStateSaveTimer) {
      clearTimeout(this.pendingStateSaveTimer);
      this.pendingStateSaveTimer = null;
    }

    this.pendingStateSaveRequest = null;
  }
}

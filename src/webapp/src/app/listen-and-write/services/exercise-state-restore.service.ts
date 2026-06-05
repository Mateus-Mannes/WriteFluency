import { Injectable, signal } from '@angular/core';
import { TextComparisonResult } from 'src/api/listen-and-write';
import { Proposition } from '../../../api/listen-and-write/model/proposition';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { BrowserService } from '../../core/services/browser.service';
import { ProgressStateResponse } from '../../user/models/user-progress.model';
import { ExerciseProgressTrackingService } from './exercise-progress-tracking.service';
import * as constants from '../listen-and-write.constants';
import * as models from '../listen-and-write.models';

export interface RestoreExerciseStateRequest {
  exerciseId: number | null;
  getStateKey(exerciseId: number): string | null;
  getExerciseStateKey(exerciseId: number): string | null;
  applySnapshot(snapshot: models.RestoredExerciseSnapshot): void;
  resetPendingPausedTime(): void;
}

@Injectable()
export class ExerciseStateRestoreService {
  readonly isRestoring = signal<boolean>(false);

  private restoreRequestToken = 0;
  private activeExerciseId: number | null = null;
  private shouldSyncCompletedResultAfterRestore = false;

  constructor(
    private authSessionStore: AuthSessionStore,
    private browserService: BrowserService,
    private exerciseProgressTracking: ExerciseProgressTrackingService,
  ) {}

  resetForExercise(exerciseId: number | null): void {
    this.activeExerciseId = exerciseId;
    this.restoreRequestToken += 1;
    this.shouldSyncCompletedResultAfterRestore = false;
    this.isRestoring.set(false);
  }

  async restore(request: RestoreExerciseStateRequest): Promise<void> {
    const exerciseId = request.exerciseId;
    if (!exerciseId) {
      return;
    }

    const restoreToken = ++this.restoreRequestToken;
    this.activeExerciseId = exerciseId;
    request.resetPendingPausedTime();

    if (!this.authSessionStore.isAuthenticated()) {
      this.isRestoring.set(false);
      this.restoreStateFromLocalStorage(exerciseId, restoreToken, request);
      return;
    }

    this.isRestoring.set(true);

    try {
      const serverState = await this.loadServerStateWithTimeout(exerciseId);
      if (!this.isRestoreTokenActive(restoreToken, exerciseId)) {
        return;
      }

      const localSnapshot = this.readLocalExerciseSnapshot(exerciseId, request);
      const shouldSyncCompletedAfterLogin = this.consumePendingCompletedSyncRequest(exerciseId, localSnapshot);
      if (shouldSyncCompletedAfterLogin) {
        request.applySnapshot(this.toRestoredSnapshot(localSnapshot!));
        this.shouldSyncCompletedResultAfterRestore = true;
        return;
      }

      if (this.shouldPreferLocalCompletedState(serverState, localSnapshot)) {
        request.applySnapshot(this.toRestoredSnapshot(localSnapshot!));
        this.shouldSyncCompletedResultAfterRestore = false;
        return;
      }

      if (serverState) {
        this.shouldSyncCompletedResultAfterRestore = false;
        request.applySnapshot(this.toRestoredServerSnapshot(serverState));
        return;
      }

      if (localSnapshot) {
        this.shouldSyncCompletedResultAfterRestore = false;
        request.applySnapshot(this.toRestoredSnapshot(localSnapshot));
      } else {
        this.shouldSyncCompletedResultAfterRestore = false;
        this.restoreStateFromLocalStorage(exerciseId, restoreToken, request);
      }
    } finally {
      if (this.isRestoreTokenActive(restoreToken, exerciseId)) {
        this.isRestoring.set(false);
      }
    }
  }

  setPendingCompletedSyncRequest(exerciseId: number | null): void {
    if (!exerciseId || !this.browserService.isBrowserEnvironment()) {
      return;
    }

    try {
      window.sessionStorage.setItem(
        constants.postLoginCompleteSyncStorageKey,
        JSON.stringify({
          exerciseId,
          createdAtUtc: new Date().toISOString(),
        }));
    } catch {
      // noop
    }
  }

  syncCompletedResultAfterRestoreIfNeeded(params: {
    proposition: Proposition | null;
    exerciseState: models.ExerciseState;
    result: TextComparisonResult | null;
  }): void {
    if (!params.proposition) {
      return;
    }

    if (!this.shouldSyncCompletedResultAfterRestore || !this.authSessionStore.isAuthenticated()) {
      return;
    }

    this.shouldSyncCompletedResultAfterRestore = false;

    if (params.exerciseState !== 'results' || !params.result) {
      return;
    }

    this.exerciseProgressTracking.trackComplete(params.proposition, params.result);
  }

  private async loadServerStateWithTimeout(exerciseId: number) {
    let timeoutHandle: ReturnType<typeof setTimeout> | null = null;
    const timeoutPromise = new Promise<null>((resolve) => {
      timeoutHandle = setTimeout(() => resolve(null), constants.restoreServerStateTimeoutMs);
    });

    try {
      return await Promise.race([
        this.exerciseProgressTracking.loadState(exerciseId),
        timeoutPromise,
      ]);
    } finally {
      if (timeoutHandle) {
        clearTimeout(timeoutHandle);
      }
    }
  }

  private restoreStateFromLocalStorage(
    exerciseId: number,
    restoreToken: number,
    request: RestoreExerciseStateRequest,
  ): void {
    if (!this.isRestoreTokenActive(restoreToken, exerciseId)) {
      return;
    }

    const localSnapshot = this.readLocalExerciseSnapshot(exerciseId, request);
    if (!localSnapshot) {
      return;
    }

    request.applySnapshot(this.toRestoredSnapshot(localSnapshot));
  }

  private readLocalExerciseSnapshot(
    exerciseId: number,
    request: RestoreExerciseStateRequest,
  ): models.LocalExerciseSnapshot | null {
    const stateKey = request.getStateKey(exerciseId);
    const exerciseState = stateKey
      ? this.normalizeExerciseState(this.browserService.getItem(stateKey))
      : null;

    const exerciseStateKey = request.getExerciseStateKey(exerciseId);
    if (!exerciseStateKey) {
      return null;
    }

    const serializedState = this.browserService.getItem(exerciseStateKey);
    if (!serializedState) {
      return exerciseState
        ? {
            state: exerciseState,
            userText: null,
            autoPauseSeconds: null,
            pausedTimeSeconds: null,
            result: null,
            savedAtUtc: null,
          }
        : null;
    }

    try {
      const parsed = JSON.parse(serializedState) as {
        userText?: string | null;
        autoPause?: number | null;
        pausedTime?: number | null;
        result?: TextComparisonResult | null;
        savedAtUtc?: string | null;
      };

      return {
        state: exerciseState,
        userText: parsed.userText || null,
        autoPauseSeconds: parsed.autoPause ?? null,
        pausedTimeSeconds: parsed.pausedTime ?? null,
        result: parsed.result ?? null,
        savedAtUtc: parsed.savedAtUtc ?? null,
      };
    } catch {
      this.browserService.removeItem(exerciseStateKey);
      return exerciseState
        ? {
            state: exerciseState,
            userText: null,
            autoPauseSeconds: null,
            pausedTimeSeconds: null,
            result: null,
            savedAtUtc: null,
          }
        : null;
    }
  }

  private shouldPreferLocalCompletedState(
    serverState: ProgressStateResponse | null,
    localSnapshot: models.LocalExerciseSnapshot | null,
  ): boolean {
    if (!serverState || !localSnapshot) {
      return false;
    }

    if (localSnapshot.state !== 'results' || !localSnapshot.result) {
      return false;
    }

    const localSavedAtMs = this.parseTimestamp(localSnapshot.savedAtUtc);
    const serverUpdatedAtMs = this.parseTimestamp(serverState.updatedAtUtc);
    if (localSavedAtMs !== null && serverUpdatedAtMs !== null) {
      return localSavedAtMs > serverUpdatedAtMs;
    }

    if (localSavedAtMs !== null && serverUpdatedAtMs === null) {
      return true;
    }

    const normalizedServerState = this.normalizeExerciseState(serverState.exerciseState);
    const serverHasDraftText = Boolean(serverState.userText?.trim());
    const serverHasRestorableDraft =
      normalizedServerState === 'exercise'
      || serverHasDraftText
      || serverState.autoPauseSeconds !== null
      || serverState.pausedTimeSeconds !== null;

    return !serverHasRestorableDraft;
  }

  private consumePendingCompletedSyncRequest(
    exerciseId: number,
    localSnapshot: models.LocalExerciseSnapshot | null,
  ): boolean {
    if (!this.browserService.isBrowserEnvironment()) {
      return false;
    }

    let pendingExerciseId: number | null = null;
    try {
      const rawValue = window.sessionStorage.getItem(constants.postLoginCompleteSyncStorageKey);
      if (!rawValue) {
        return false;
      }

      const parsed = JSON.parse(rawValue) as { exerciseId?: unknown };
      if (typeof parsed.exerciseId === 'number' && Number.isFinite(parsed.exerciseId)) {
        pendingExerciseId = parsed.exerciseId;
      }
    } catch {
      // noop
    }

    if (pendingExerciseId !== exerciseId) {
      return false;
    }

    try {
      window.sessionStorage.removeItem(constants.postLoginCompleteSyncStorageKey);
    } catch {
      // noop
    }

    return localSnapshot?.state === 'results' && localSnapshot.result !== null;
  }

  private toRestoredServerSnapshot(serverState: ProgressStateResponse): models.RestoredExerciseSnapshot {
    const restoredExerciseState = this.normalizeExerciseState(serverState.exerciseState);
    return {
      state: restoredExerciseState,
      userText: serverState.userText ?? null,
      autoPauseSeconds: serverState.autoPauseSeconds ?? 2,
      pausedTimeSeconds: serverState.pausedTimeSeconds,
      result: this.buildRestoredResult(serverState, restoredExerciseState),
    };
  }

  private buildRestoredResult(
    serverState: ProgressStateResponse,
    restoredExerciseState: models.ExerciseState | null,
  ): TextComparisonResult | null {
    if (restoredExerciseState !== 'results') {
      return null;
    }

    return {
      originalText: serverState.originalText ?? null,
      userText: serverState.userText,
      comparisons: serverState.comparisons ?? [],
      accuracyPercentage: serverState.accuracyPercentage ?? 0,
    };
  }

  private toRestoredSnapshot(localSnapshot: models.LocalExerciseSnapshot): models.RestoredExerciseSnapshot {
    return {
      state: localSnapshot.state,
      userText: localSnapshot.userText,
      autoPauseSeconds: localSnapshot.autoPauseSeconds ?? 2,
      pausedTimeSeconds: localSnapshot.pausedTimeSeconds,
      result: localSnapshot.result,
    };
  }

  private parseTimestamp(value: string | null | undefined): number | null {
    if (!value) {
      return null;
    }

    const parsed = Date.parse(value);
    if (!Number.isFinite(parsed)) {
      return null;
    }

    return parsed;
  }

  private isRestoreTokenActive(restoreToken: number, exerciseId: number): boolean {
    return this.restoreRequestToken === restoreToken && this.activeExerciseId === exerciseId;
  }

  private normalizeExerciseState(value: string | null | undefined): models.ExerciseState | null {
    if (!value) {
      return null;
    }

    if (value === 'intro' || value === 'exercise' || value === 'results') {
      return value;
    }

    return null;
  }
}

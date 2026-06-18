import { Injectable, signal } from '@angular/core';
import { TextComparisonResult } from 'src/api/listen-and-write';
import { Proposition } from '../../../api/listen-and-write/model/proposition';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { BrowserService } from '../../core/services/browser.service';
import { GuestExerciseProgressTransferService } from '../../core/services/guest-exercise-progress-transfer.service';
import { ProgressStateResponse } from '../../user/models/user-progress.model';
import { ExerciseLocalStateStorageService } from './exercise-local-state-storage.service';
import { ExerciseProgressTrackingService } from './exercise-progress-tracking.service';
import * as constants from '../listen-and-write.constants';
import * as models from '../listen-and-write.models';

export interface RestoreExerciseStateRequest {
  exerciseId: number | null;
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
    private localStateStorage: ExerciseLocalStateStorageService,
    private guestProgressTransfer: GuestExerciseProgressTransferService,
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

      const transferredGuestSnapshot = this.readAuthorizedGuestCompletedSnapshot(exerciseId);
      if (transferredGuestSnapshot) {
        this.migrateGuestSnapshotToCurrentUser(exerciseId);
        request.applySnapshot(this.toRestoredSnapshot(transferredGuestSnapshot));
        this.shouldSyncCompletedResultAfterRestore = true;
        return;
      }

      const localSnapshot = this.readCurrentLocalExerciseSnapshot(exerciseId);
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

    const localSnapshot = this.readCurrentLocalExerciseSnapshot(exerciseId);
    if (!localSnapshot) {
      return;
    }

    request.applySnapshot(this.toRestoredSnapshot(localSnapshot));
  }

  private readCurrentLocalExerciseSnapshot(exerciseId: number): models.LocalExerciseSnapshot | null {
    return this.readLocalExerciseSnapshot(
      this.localStateStorage.getCurrentStateKey(exerciseId),
      this.localStateStorage.getCurrentSnapshotKey(exerciseId),
    );
  }

  private readGuestLocalExerciseSnapshot(exerciseId: number): models.LocalExerciseSnapshot | null {
    return this.readLocalExerciseSnapshot(
      this.localStateStorage.getGuestStateKey(exerciseId),
      this.localStateStorage.getGuestSnapshotKey(exerciseId),
    );
  }

  private readLocalExerciseSnapshot(
    stateKey: string | null,
    exerciseStateKey: string | null,
  ): models.LocalExerciseSnapshot | null {
    const exerciseState = stateKey
      ? this.normalizeExerciseState(this.browserService.getItem(stateKey))
      : null;

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

  private readAuthorizedGuestCompletedSnapshot(exerciseId: number): models.LocalExerciseSnapshot | null {
    const userId = this.authSessionStore.userId();
    if (!this.guestProgressTransfer.consumeAuthorizedTransfer(exerciseId, userId)) {
      return null;
    }

    const guestSnapshot = this.readGuestLocalExerciseSnapshot(exerciseId);
    return guestSnapshot?.state === 'results' && guestSnapshot.result
      ? guestSnapshot
      : null;
  }

  private migrateGuestSnapshotToCurrentUser(exerciseId: number): void {
    const guestStateKey = this.localStateStorage.getGuestStateKey(exerciseId);
    const guestSnapshotKey = this.localStateStorage.getGuestSnapshotKey(exerciseId);
    const currentStateKey = this.localStateStorage.getCurrentStateKey(exerciseId);
    const currentSnapshotKey = this.localStateStorage.getCurrentSnapshotKey(exerciseId);

    if (!currentStateKey || !currentSnapshotKey) {
      return;
    }

    const state = this.browserService.getItem(guestStateKey);
    const snapshot = this.browserService.getItem(guestSnapshotKey);
    if (state) {
      this.browserService.setItem(currentStateKey, state);
    }
    if (snapshot) {
      this.browserService.setItem(currentSnapshotKey, snapshot);
    }

    this.browserService.removeItem(guestStateKey);
    this.browserService.removeItem(guestSnapshotKey);
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

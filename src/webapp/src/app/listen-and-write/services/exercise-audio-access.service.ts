import { Injectable, signal } from '@angular/core';
import { BeginExerciseResultDto, PreviewExerciseAccessResultDto } from 'src/api/listen-and-write';
import { Proposition } from '../../../api/listen-and-write/model/proposition';
import { PropositionsService } from '../../../api/listen-and-write/api/propositions.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import * as constants from '../listen-and-write.constants';
import * as models from '../listen-and-write.models';

export interface AudioAccessCallbacks {
  onMetadata(metadata: Proposition): void;
  onProRequired(accessStatus?: string): void;
  onGranted(context?: models.BeginExerciseContext): void;
  onMissingAudio(startWritingWhenGranted: boolean): void;
  onError(error: unknown, startWritingWhenGranted: boolean): void;
}

export interface AudioAccessRequest {
  exerciseId: number;
  startWritingWhenGranted: boolean;
  context?: models.BeginExerciseContext;
  callbacks: AudioAccessCallbacks;
}

export interface HydratedAudioAccessRequest {
  exerciseId: number | null;
  hasHydrated: boolean;
  isBrowserEnvironment: boolean;
  hasProposition: boolean;
  isPro: boolean;
  requiresPro: boolean;
  exerciseState: models.ExerciseState;
  callbacks: AudioAccessCallbacks;
}

export type AudioAccessMode = 'none' | 'begin_granted' | 'preview_only';

@Injectable()
export class ExerciseAudioAccessService {
  readonly audioUrl = signal<string | null>(null);
  readonly audioAccessMode = signal<AudioAccessMode>('none');
  readonly isResolving = signal<boolean>(false);
  readonly isBeginningExercise = signal<boolean>(false);
  private requestToken = 0;
  private resolvedExerciseId: number | null = null;
  private audioExpiresAtUtc: string | null = null;

  constructor(
    private propositionsService: PropositionsService,
    private exerciseSessionTracking: ExerciseSessionTrackingService,
  ) {}

  getAudioExpiresAtUtc(): string | null {
    return this.audioExpiresAtUtc;
  }

  reset(): void {
    this.audioUrl.set(null);
    this.audioAccessMode.set('none');
    this.audioExpiresAtUtc = null;
    this.resolvedExerciseId = null;
    this.requestToken += 1;
    this.isResolving.set(false);
    this.isBeginningExercise.set(false);
  }

  tryResolveAfterHydration(request: HydratedAudioAccessRequest): void {
    if (!request.hasHydrated || !request.isBrowserEnvironment) {
      return;
    }

    const exerciseId = request.exerciseId;
    if (!exerciseId || !request.hasProposition) {
      return;
    }

    if (request.exerciseState === 'results') {
      return;
    }

    if (
      this.audioUrl()
      || this.isResolving()
      || this.resolvedExerciseId === exerciseId
    ) {
      return;
    }

    if (request.requiresPro && !request.isPro) {
      this.preview({
        exerciseId,
        startWritingWhenGranted: false,
        callbacks: request.callbacks,
      });
      return;
    }

    this.resolve({
      exerciseId,
      startWritingWhenGranted: false,
      callbacks: request.callbacks,
    });
  }

  canStartWithResolvedAudio(): boolean {
    return Boolean(this.audioUrl()) && this.audioAccessMode() === 'begin_granted';
  }

  resolve(request: AudioAccessRequest): void {
    const requestToken = ++this.requestToken;
    this.isResolving.set(true);
    this.isBeginningExercise.set(request.startWritingWhenGranted);

    this.propositionsService.apiPropositionIdBeginPost(request.exerciseId).subscribe({
      next: (result) => {
        if (!this.isCurrentRequest(requestToken)) {
          return;
        }

        this.isResolving.set(false);
        this.isBeginningExercise.set(false);
        this.handleResult(result, request);
      },
      error: (error) => {
        if (!this.isCurrentRequest(requestToken)) {
          return;
        }

        this.isResolving.set(false);
        this.isBeginningExercise.set(false);
        this.exerciseSessionTracking.trackEvent('exercise_audio_access_failed', {
          error: error?.message ?? 'unknown_error',
        });
        request.callbacks.onError(error, request.startWritingWhenGranted);
      },
    });
  }

  private preview(request: AudioAccessRequest): void {
    const requestToken = ++this.requestToken;
    this.isResolving.set(true);
    this.isBeginningExercise.set(false);

    this.propositionsService.apiPropositionIdPreviewAccessPost(request.exerciseId).subscribe({
      next: (result) => {
        if (!this.isCurrentRequest(requestToken)) {
          return;
        }

        this.isResolving.set(false);
        this.isBeginningExercise.set(false);
        this.handlePreviewResult(result, request);
      },
      error: (error) => {
        if (!this.isCurrentRequest(requestToken)) {
          return;
        }

        this.isResolving.set(false);
        this.isBeginningExercise.set(false);
        this.exerciseSessionTracking.trackEvent('exercise_audio_access_failed', {
          error: error?.message ?? 'unknown_error',
        });
        request.callbacks.onError(error, false);
      },
    });
  }

  private isCurrentRequest(requestToken: number): boolean {
    return this.requestToken === requestToken;
  }

  private handleResult(
    result: BeginExerciseResultDto,
    request: AudioAccessRequest,
  ): void {
    this.resolvedExerciseId = request.exerciseId;

    if (result.metadata) {
      request.callbacks.onMetadata(result.metadata);
    }

    if (this.isDeniedAccess(result.access)) {
      this.clearAudioAccess();
      request.callbacks.onProRequired(result.access);
      return;
    }

    if (result.access !== constants.accessGranted || !result.audioUrl) {
      this.audioAccessMode.set('none');
      this.exerciseSessionTracking.trackEvent('begin_exercise_failed', {
        error: 'missing_audio_url',
      });
      request.callbacks.onMissingAudio(request.startWritingWhenGranted);
      return;
    }

    this.audioUrl.set(result.audioUrl);
    this.audioAccessMode.set('begin_granted');
    this.audioExpiresAtUtc = result.audioExpiresAtUtc ?? null;

    if (request.startWritingWhenGranted) {
      request.callbacks.onGranted(request.context);
    }
  }

  private handlePreviewResult(
    result: PreviewExerciseAccessResultDto,
    request: AudioAccessRequest,
  ): void {
    this.resolvedExerciseId = request.exerciseId;

    if (result.metadata) {
      request.callbacks.onMetadata(result.metadata);
    }

    if (this.isDeniedAccess(result.accessStatus)) {
      this.clearAudioAccess();
      request.callbacks.onProRequired(result.accessStatus);
      return;
    }

    if (!result.audioUrl) {
      this.audioAccessMode.set('none');
      this.exerciseSessionTracking.trackEvent('preview_exercise_access_failed', {
        error: 'missing_audio_url',
      });
      request.callbacks.onMissingAudio(false);
      return;
    }

    this.audioUrl.set(result.audioUrl);
    this.audioAccessMode.set('preview_only');
    this.audioExpiresAtUtc = result.audioExpiresAtUtc ?? null;
  }

  private clearAudioAccess(): void {
    this.audioUrl.set(null);
    this.audioAccessMode.set('none');
    this.audioExpiresAtUtc = null;
  }

  private isDeniedAccess(access: string | null | undefined): boolean {
    return access === constants.proRequiredAccess
      || access === constants.catalogLoginRequiredAccess
      || access === constants.catalogUpgradeRequiredAccess;
  }
}

import { Injectable, signal } from '@angular/core';
import { BeginExerciseResultDto } from 'src/api/listen-and-write';
import { Proposition } from '../../../api/listen-and-write/model/proposition';
import { PropositionsService } from '../../../api/listen-and-write/api/propositions.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import * as constants from '../listen-and-write.constants';
import * as models from '../listen-and-write.models';

export interface AudioAccessCallbacks {
  onMetadata(metadata: Proposition): void;
  onProRequired(): void;
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
  exerciseState: models.ExerciseState;
  callbacks: AudioAccessCallbacks;
}

@Injectable()
export class ExerciseAudioAccessService {
  readonly audioUrl = signal<string | null>(null);
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

    this.resolve({
      exerciseId,
      startWritingWhenGranted: false,
      callbacks: request.callbacks,
    });
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

    if (result.access === constants.proRequiredAccess) {
      this.audioUrl.set(null);
      this.audioExpiresAtUtc = null;
      request.callbacks.onProRequired();
      return;
    }

    if (result.access !== constants.accessGranted || !result.audioUrl) {
      this.exerciseSessionTracking.trackEvent('begin_exercise_failed', {
        error: 'missing_audio_url',
      });
      request.callbacks.onMissingAudio(request.startWritingWhenGranted);
      return;
    }

    this.audioUrl.set(result.audioUrl);
    this.audioExpiresAtUtc = result.audioExpiresAtUtc ?? null;

    if (request.startWritingWhenGranted) {
      request.callbacks.onGranted(request.context);
    }
  }
}

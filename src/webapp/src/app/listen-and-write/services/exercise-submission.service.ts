import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Proposition } from '../../../api/listen-and-write/model/proposition';
import { BrowserService } from '../../core/services/browser.service';
import { TextComparisonResult, TextComparisonsService } from 'src/api/listen-and-write';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import * as constants from '../listen-and-write.constants';
import * as models from '../listen-and-write.models';

export interface SubmitAudioState {
  audioEnded: boolean;
  currentTime: number | null;
  duration: number | null;
}

export interface ExerciseSubmissionRequest {
  proposition: Proposition | null;
  exerciseId: number | null;
  submittedUserText: string;
  exerciseTimeUsedMs: number | null;
  onSuccess(result: TextComparisonResult, submittedUserText: string): void;
  onProRequired(): void;
  onFailure(): void;
}

@Injectable()
export class ExerciseSubmissionService {
  readonly isSubmitting = signal<boolean>(false);

  constructor(
    private textComparisonsService: TextComparisonsService,
    private browserService: BrowserService,
    private exerciseSessionTracking: ExerciseSessionTrackingService,
  ) {}

  trackSubmitClicked(hasSubmitWarning: boolean): void {
    this.exerciseSessionTracking.trackEvent('exercise_submit_clicked', {
      has_submit_warning: hasSubmitWarning,
    });
  }

  getSubmitWarningMessage(
    audioState: SubmitAudioState,
    originalWordCount: number | null | undefined,
    userWordCount: number,
  ): string | null {
    const warnings: string[] = [];

    if (!this.hasCompletedAudioPlayback(audioState)) {
      warnings.push('We strongly recommend listening through the audio before submitting. You can skip words or write what you think you heard.');
      warnings.push('If you submit too early, the accuracy percentage can be less precise and you may lose points.');
      warnings.push('If auto-pause is enabled, press play again after each pause using Ctrl/Cmd + Enter.');
    }

    const minimumWords = this.getMinimumWordsForSubmit(originalWordCount);
    if (minimumWords > 0 && userWordCount < minimumWords) {
      warnings.push(`Your text is still short (${userWordCount} words). Partial answers can reduce correction accuracy.`);
    }

    if (warnings.length === 0) {
      return null;
    }

    warnings.unshift('Goal: write as much of the full audio text as you can.');

    return `Quick reminder before submitting:\n\n- ${warnings.join('\n\n- ')}`;
  }

  submit(request: ExerciseSubmissionRequest): void {
    const proposition = request.proposition;
    if (!proposition) {
      this.exerciseSessionTracking.trackEvent('exercise_submit_failed', {
        reason: 'missing_proposition',
      });
      request.onFailure();
      return;
    }

    if (!proposition.id) {
      this.exerciseSessionTracking.trackEvent('exercise_submit_failed', {
        reason: 'missing_proposition_id',
      });
      request.onFailure();
      return;
    }

    this.isSubmitting.set(true);
    this.exerciseSessionTracking.markSubmitted();
    this.exerciseSessionTracking.trackTextChanged(request.submittedUserText);

    const submitConfirmedMetadata = this.buildSubmitTelemetryMetadata({
      exerciseId: request.exerciseId,
      propositionId: proposition.id,
      userText: request.submittedUserText,
      originalText: null,
      accuracyPercentage: null,
      exerciseTimeUsedMs: request.exerciseTimeUsedMs,
    });
    this.exerciseSessionTracking.trackEvent('exercise_submit_confirmed', {
      ...submitConfirmedMetadata.properties,
      text_snapshot: request.submittedUserText.slice(0, constants.submitTextSnapshotMaxLength),
      text_truncated: request.submittedUserText.length > constants.submitTextSnapshotMaxLength,
    }, {
      ...submitConfirmedMetadata.measurements,
      text_char_count: request.submittedUserText.length,
      text_word_count: this.countWords(request.submittedUserText),
    });

    const submitRequestedAtMs = Date.now();
    this.textComparisonsService.apiTextComparisonCompareTextsPost({
      propositionId: proposition.id,
      userText: request.submittedUserText,
    }).subscribe({
      next: (result: TextComparisonResult) => {
        const apiElapsedMs = Date.now() - submitRequestedAtMs;
        const remainingTime = Math.max(0, constants.submitMinLoadingMs - apiElapsedMs);

        setTimeout(() => {
          const finalUserText = result.userText ?? request.submittedUserText;
          const finalOriginalText = result.originalText ?? null;
          const submitSuccessMetadata = this.buildSubmitTelemetryMetadata({
            exerciseId: request.exerciseId,
            propositionId: proposition.id,
            userText: finalUserText,
            originalText: finalOriginalText,
            accuracyPercentage: result.accuracyPercentage,
            exerciseTimeUsedMs: request.exerciseTimeUsedMs,
          });

          this.trackExerciseSubmitConversion(result.accuracyPercentage);
          this.exerciseSessionTracking.trackEvent('exercise_submit_succeeded', {
            ...submitSuccessMetadata.properties,
            comparison_count: result.comparisons?.length ?? 0,
          }, {
            ...submitSuccessMetadata.measurements,
            submit_api_latency_ms: apiElapsedMs,
            submit_flow_elapsed_ms: Date.now() - submitRequestedAtMs,
          });
          this.isSubmitting.set(false);
          request.onSuccess(result, request.submittedUserText);
        }, remainingTime);
      },
      error: (error) => {
        const apiElapsedMs = Date.now() - submitRequestedAtMs;
        const remainingTime = Math.max(0, constants.submitMinLoadingMs - apiElapsedMs);

        setTimeout(() => {
          const submitFailureMetadata = this.buildSubmitTelemetryMetadata({
            exerciseId: request.exerciseId,
            propositionId: proposition.id,
            userText: request.submittedUserText,
            originalText: null,
            accuracyPercentage: null,
            exerciseTimeUsedMs: request.exerciseTimeUsedMs,
          });

          this.exerciseSessionTracking.trackEvent('exercise_submit_failed', {
            ...submitFailureMetadata.properties,
            error: error?.message ?? 'unknown_error',
          }, {
            ...submitFailureMetadata.measurements,
            submit_api_latency_ms: apiElapsedMs,
            submit_flow_elapsed_ms: Date.now() - submitRequestedAtMs,
          });
          this.isSubmitting.set(false);

          if (this.isProRequiredError(error)) {
            request.onProRequired();
          } else {
            request.onFailure();
          }
        }, remainingTime);
      },
    });
  }

  hasCompletedAudioPlayback(audioState: SubmitAudioState): boolean {
    if (audioState.audioEnded) {
      return true;
    }

    const duration = audioState.duration;
    if (!Number.isFinite(duration) || (duration ?? 0) <= 0) {
      return false;
    }

    const currentTime = audioState.currentTime ?? 0;
    return currentTime >= Math.max(0, duration! - constants.submitAudioRemainingToleranceSeconds);
  }

  private getMinimumWordsForSubmit(originalWordCount: number | null | undefined): number {
    if (!Number.isFinite(originalWordCount) || (originalWordCount ?? 0) <= 0) {
      return 0;
    }

    return Math.max(3, Math.ceil(originalWordCount! * 0.2));
  }

  private isProRequiredError(error: unknown): boolean {
    return error instanceof HttpErrorResponse
      && error.status === 403
      && error.error?.access === constants.proRequiredAccess;
  }

  private trackExerciseSubmitConversion(accuracyPercentage: number | null | undefined): void {
    if (!Number.isFinite(accuracyPercentage) || (accuracyPercentage ?? 0) < 0.1) {
      return;
    }

    if (!this.browserService.isBrowserEnvironment()) {
      return;
    }

    const gtag = (globalThis as typeof globalThis & { gtag?: models.GtagEvent }).gtag;
    if (typeof gtag !== 'function') {
      return;
    }

    gtag('event', 'conversion', {
      send_to: constants.exerciseSubmitConversionSendTo,
      value: 1.0,
      currency: 'BRL',
    });
  }

  private buildSubmitTelemetryMetadata(params: {
    exerciseId: number | null;
    propositionId: number | null | undefined;
    userText: string | null | undefined;
    originalText: string | null | undefined;
    accuracyPercentage: number | null | undefined;
    exerciseTimeUsedMs: number | null;
  }): {
    properties: Record<string, string | number | boolean>;
    measurements: Record<string, number>;
  } {
    const normalizedUserText = params.userText ?? '';
    const normalizedOriginalText = params.originalText ?? '';
    const userTextTelemetry = this.toTelemetryText(normalizedUserText);
    const originalTextTelemetry = this.toTelemetryText(normalizedOriginalText);

    const properties: Record<string, string | number | boolean> = {
      exercise_id: params.exerciseId ?? params.propositionId ?? '',
      proposition_id: params.propositionId ?? '',
      user_text: userTextTelemetry.text,
      user_text_truncated: userTextTelemetry.truncated,
      original_text: originalTextTelemetry.text,
      original_text_truncated: originalTextTelemetry.truncated,
      has_exercise_time_used: params.exerciseTimeUsedMs !== null,
    };

    const measurements: Record<string, number> = {
      user_text_char_count: normalizedUserText.length,
      user_text_word_count: this.countWords(normalizedUserText),
      original_text_char_count: normalizedOriginalText.length,
      original_text_word_count: this.countWords(normalizedOriginalText),
    };

    if (params.exerciseTimeUsedMs !== null) {
      measurements['exercise_time_used_ms'] = params.exerciseTimeUsedMs;
      measurements['exercise_time_used_seconds'] = Number((params.exerciseTimeUsedMs / 1000).toFixed(2));
    }

    if (typeof params.accuracyPercentage === 'number' && Number.isFinite(params.accuracyPercentage)) {
      measurements['accuracy_percentage'] = params.accuracyPercentage;
    }

    return {
      properties,
      measurements,
    };
  }

  private toTelemetryText(text: string): { text: string; truncated: boolean } {
    if (text.length <= constants.submitTelemetryTextMaxLength) {
      return { text, truncated: false };
    }

    return {
      text: text.slice(0, constants.submitTelemetryTextMaxLength),
      truncated: true,
    };
  }

  private countWords(text: string | null | undefined): number {
    return (text || '').trim().split(/\s+/).filter(Boolean).length;
  }
}

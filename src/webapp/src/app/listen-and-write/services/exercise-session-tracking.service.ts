import { Injectable, Optional } from '@angular/core';
import { BrowserService } from '../../core/services/browser.service';
import { Insights, InsightsMeasurements, InsightsProperties } from '../../../telemetry/insights.service';

interface ExerciseSessionContext {
  sessionId: string;
  operationId: string;
  startedAtMs: number;
  eventSequence: number;
  exerciseId: number | null;
  hasSubmitted: boolean;
}

type TrackPropertyValue = string | number | boolean | null | undefined;

@Injectable({ providedIn: 'root' })
export class ExerciseSessionTrackingService {
  private static readonly TEXT_TRACK_MIN_INTERVAL_MS = 1500;
  private static readonly TEXT_TRACK_MIN_LENGTH_DELTA = 15;
  private static readonly TEXT_TRACK_MAX_EVENTS = 120;
  private static readonly TEXT_SNAPSHOT_MAX_LENGTH = 1200;

  private session: ExerciseSessionContext | null = null;

  private lastTrackedText = '';
  private lastTrackedTextAtMs = 0;
  private trackedTextEvents = 0;

  constructor(
    private browserService: BrowserService,
    @Optional() private insights: Insights | null
  ) { }

  private isTrackingEnabled(): boolean {
    return this.insights !== null;
  }

  hasActiveSession(): boolean {
    return this.session !== null;
  }

  getCurrentSessionId(): string | null {
    return this.session?.sessionId ?? null;
  }

  getCurrentOperationId(): string | null {
    return this.session?.operationId ?? null;
  }

  startSession(params: { exerciseId: number | null; source?: string }): void {
    if (!this.browserService.isBrowserEnvironment() || !this.isTrackingEnabled()) {
      this.session = null;
      this.resetTextTrackingState();
      return;
    }

    if (this.session) {
      this.endSession('restarted');
    }

    const now = Date.now();
    const operationId = this.generateHexId(32);
    const sessionId = `${now}-${this.generateHexId(8)}`;

    this.session = {
      sessionId,
      operationId,
      startedAtMs: now,
      eventSequence: 0,
      exerciseId: params.exerciseId ?? null,
      hasSubmitted: false
    };

    this.resetTextTrackingState();
    this.trackEvent('exercise_session_started', {
      source: params.source ?? 'listen-and-write',
    });
  }

  endSession(reason: string): void {
    if (!this.session) {
      return;
    }

    this.trackEvent('exercise_session_ended', {
      reason,
      has_submitted: this.session.hasSubmitted
    });

    this.session = null;
    this.resetTextTrackingState();
  }

  updateSessionContext(params: { exerciseId?: number | null }): void {
    if (!this.session) {
      return;
    }

    if (params.exerciseId !== undefined) {
      this.session.exerciseId = params.exerciseId;
    }
  }

  markSubmitted(): void {
    if (!this.session) {
      return;
    }

    this.session.hasSubmitted = true;
  }

  trackEvent(
    name: string,
    properties: Record<string, TrackPropertyValue> = {},
    measurements: InsightsMeasurements = {}
  ): void {
    if (!this.session || !this.isTrackingEnabled()) {
      return;
    }

    const now = Date.now();
    this.session.eventSequence += 1;

    const sessionProperties: InsightsProperties = {
      wf_session_id: this.session.sessionId,
      wf_operation_id: this.session.operationId,
      wf_exercise_id: String(this.session.exerciseId ?? ''),
      wf_has_submitted: String(this.session.hasSubmitted),
      wf_event_sequence: String(this.session.eventSequence),
    };

    const eventProperties = this.toInsightsProperties(properties);
    const mergedProperties: InsightsProperties = {
      ...sessionProperties,
      ...eventProperties
    };

    const mergedMeasurements: InsightsMeasurements = {
      ...measurements,
      wf_elapsed_ms: now - this.session.startedAtMs
    };

    this.insights?.trackEvent(name, mergedProperties, mergedMeasurements);
  }

  trackTextChanged(text: string): void {
    if (!this.session || !this.isTrackingEnabled()) {
      return;
    }

    if (this.trackedTextEvents >= ExerciseSessionTrackingService.TEXT_TRACK_MAX_EVENTS) {
      return;
    }

    const now = Date.now();
    const textLengthDelta = Math.abs(text.length - this.lastTrackedText.length);
    const elapsedSinceLastTrack = now - this.lastTrackedTextAtMs;

    const shouldTrack =
      this.trackedTextEvents === 0 ||
      elapsedSinceLastTrack >= ExerciseSessionTrackingService.TEXT_TRACK_MIN_INTERVAL_MS ||
      textLengthDelta >= ExerciseSessionTrackingService.TEXT_TRACK_MIN_LENGTH_DELTA;

    if (!shouldTrack) {
      return;
    }

    const snapshot = text.slice(0, ExerciseSessionTrackingService.TEXT_SNAPSHOT_MAX_LENGTH);
    this.trackEvent('exercise_text_changed', {
      text_snapshot: snapshot,
      text_truncated: text.length > snapshot.length
    }, {
      text_char_count: text.length,
      text_word_count: this.countWords(text),
      text_change_events_sent: this.trackedTextEvents + 1
    });

    this.trackedTextEvents += 1;
    this.lastTrackedTextAtMs = now;
    this.lastTrackedText = text;
  }

  private resetTextTrackingState(): void {
    this.lastTrackedText = '';
    this.lastTrackedTextAtMs = 0;
    this.trackedTextEvents = 0;
  }

  private toInsightsProperties(properties: Record<string, TrackPropertyValue>): InsightsProperties {
    const formatted: InsightsProperties = {};
    for (const [key, value] of Object.entries(properties)) {
      if (value === null || value === undefined) {
        continue;
      }

      formatted[key] = String(value);
    }

    return formatted;
  }

  private countWords(text: string): number {
    return text.trim().split(/\s+/).filter(Boolean).length;
  }

  private generateHexId(length: number): string {
    if (
      this.browserService.isBrowserEnvironment() &&
      typeof crypto !== 'undefined' &&
      typeof crypto.getRandomValues === 'function'
    ) {
      const bytes = new Uint8Array(Math.ceil(length / 2));
      crypto.getRandomValues(bytes);
      return Array.from(bytes)
        .map((byte) => byte.toString(16).padStart(2, '0'))
        .join('')
        .slice(0, length);
    }

    let output = '';
    while (output.length < length) {
      output += Math.floor(Math.random() * 16).toString(16);
    }
    return output.slice(0, length);
  }
}

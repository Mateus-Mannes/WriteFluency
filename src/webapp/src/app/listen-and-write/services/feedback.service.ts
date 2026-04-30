import { Injectable, Optional } from '@angular/core';
import { BrowserService } from '../../core/services/browser.service';
import { Insights } from '../../../telemetry/insights.service';

export interface ExerciseFeedbackEvent {
  rating: number;
  tags: string[];
  comment: string | null;
  exerciseId: string;
  difficulty: string;
  topic: string;
  sessionId: string;
  userId: string;
  timestamp: string;
}

@Injectable({ providedIn: 'root' })
export class FeedbackService {
  private static readonly LAST_SUBMITTED_KEY = 'feedback_last_submitted';
  private static readonly LAST_DISMISSED_KEY = 'feedback_last_dismissed';
  private static readonly LAST_SHOWN_SESSION_KEY = 'feedback_last_shown_session';

  private static readonly SUBMITTED_COOLDOWN_DAYS = 15;
  private static readonly DISMISSED_COOLDOWN_DAYS = 7;

  private readonly sessionPromptId = this.generateSessionPromptId();

  constructor(
    private browserService: BrowserService,
    @Optional() private insights: Insights | null
  ) { }

  shouldShowPrompt(): boolean {
    if (!this.browserService.isBrowserEnvironment()) {
      return false;
    }

    const lastShownSession = this.browserService.getItem(FeedbackService.LAST_SHOWN_SESSION_KEY);
    if (lastShownSession === this.sessionPromptId) {
      return false;
    }

    if (this.isWithinCooldown(FeedbackService.LAST_SUBMITTED_KEY, FeedbackService.SUBMITTED_COOLDOWN_DAYS)) {
      return false;
    }

    if (this.isWithinCooldown(FeedbackService.LAST_DISMISSED_KEY, FeedbackService.DISMISSED_COOLDOWN_DAYS)) {
      return false;
    }

    return true;
  }

  consumePromptOpportunity(): boolean {
    if (!this.shouldShowPrompt()) {
      return false;
    }

    this.browserService.setItem(FeedbackService.LAST_SHOWN_SESSION_KEY, this.sessionPromptId);
    return true;
  }

  markDismissed(): void {
    this.browserService.setItem(FeedbackService.LAST_DISMISSED_KEY, new Date().toISOString());
  }

  submitFeedback(event: ExerciseFeedbackEvent): ExerciseFeedbackEvent {
    this.browserService.setItem(FeedbackService.LAST_SUBMITTED_KEY, event.timestamp);

    this.insights?.trackEvent(
      'exercise_feedback',
      {
        rating: String(event.rating),
        tags: JSON.stringify(event.tags),
        comment: event.comment ?? '',
        exerciseId: event.exerciseId,
        difficulty: event.difficulty,
        topic: event.topic,
        sessionId: event.sessionId,
        userId: event.userId,
        timestamp: event.timestamp
      },
      {
        rating: event.rating,
        tags_count: event.tags.length
      }
    );

    return event;
  }

  private isWithinCooldown(storageKey: string, days: number): boolean {
    const rawTimestamp = this.browserService.getItem(storageKey);
    if (!rawTimestamp) {
      return false;
    }

    const parsedTime = Date.parse(rawTimestamp);
    if (!Number.isFinite(parsedTime)) {
      return false;
    }

    const elapsedMs = Date.now() - parsedTime;
    const cooldownMs = days * 24 * 60 * 60 * 1000;
    return elapsedMs < cooldownMs;
  }

  private generateSessionPromptId(): string {
    if (!this.browserService.isBrowserEnvironment()) {
      return 'server';
    }

    return `${Date.now()}-${Math.random().toString(16).slice(2, 10)}`;
  }
}

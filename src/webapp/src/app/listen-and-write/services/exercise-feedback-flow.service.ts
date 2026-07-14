import { Injectable, signal } from '@angular/core';
import { Proposition } from 'src/api/listen-and-write';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import * as feedbackModal from '../../shared/feedback-modal/feedback-modal.component';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { ExerciseFeedbackEvent, FeedbackService } from './feedback.service';
import * as models from '../listen-and-write.models';

export interface FeedbackSubmissionContext {
  proposition: Proposition | null;
  exerciseId: number | null;
}

@Injectable()
export class ExerciseFeedbackFlowService {
  readonly isModalOpen = signal<boolean>(false);

  private pendingLeaveAction: (() => void) | null = null;
  private pendingRouteLeaveResolver: ((allow: boolean) => void) | null = null;

  constructor(
    private readonly authSessionStore: AuthSessionStore,
    private readonly exerciseSessionTracking: ExerciseSessionTrackingService,
    private readonly feedbackService: FeedbackService,
  ) {}

  destroy(): void {
    if (this.pendingRouteLeaveResolver) {
      this.pendingRouteLeaveResolver(true);
      this.pendingRouteLeaveResolver = null;
    }

    this.pendingLeaveAction = null;
  }

  canDeactivateFromRoute(exerciseState: models.ExerciseState): boolean | Promise<boolean> {
    if (!this.shouldPromptFeedbackOnLeave(exerciseState)) {
      return true;
    }

    if (!this.openModalIfEligible()) {
      return true;
    }

    return new Promise<boolean>((resolve) => {
      this.pendingRouteLeaveResolver = resolve;
    });
  }

  attemptLeaveResults(exerciseState: models.ExerciseState, action: () => void): void {
    if (!this.shouldPromptFeedbackOnLeave(exerciseState)) {
      action();
      return;
    }

    if (!this.openModalIfEligible()) {
      action();
      return;
    }

    this.pendingLeaveAction = action;
  }

  onDismissed(reason: 'not_now' | 'close'): void {
    this.exerciseSessionTracking.trackEvent('feedback_modal_dismissed', {
      reason,
    });
    this.feedbackService.markDismissed();
    this.closeModal();
    this.continuePendingLeaveFlow(true);
  }

  onSubmitted(
    submission: feedbackModal.FeedbackModalSubmission,
    context: FeedbackSubmissionContext,
  ): void {
    const feedbackEvent: ExerciseFeedbackEvent = {
      rating: submission.rating,
      tags: submission.tags,
      comment: submission.comment,
      exerciseId: String(context.proposition?.id ?? context.exerciseId ?? ''),
      difficulty: context.proposition?.complexityId ?? '',
      topic: context.proposition?.subjectId ?? '',
      sessionId: this.exerciseSessionTracking.getCurrentSessionId() ?? '',
      userId: this.authSessionStore.userId() ?? '',
      timestamp: new Date().toISOString(),
    };

    this.exerciseSessionTracking.trackEvent('feedback_modal_submitted', {
      rating: submission.rating,
      tags_count: submission.tags.length,
      has_comment: Boolean(submission.comment),
    }, {
      feedback_rating: submission.rating,
      feedback_tags_count: submission.tags.length,
      feedback_comment_length: submission.comment?.length ?? 0,
    });

    this.feedbackService.submitFeedback(feedbackEvent);
  }

  onInteraction(event: feedbackModal.FeedbackModalInteractionEvent): void {
    this.exerciseSessionTracking.trackEvent('feedback_modal_interaction', {
      action: event.action,
      rating: event.rating,
      tag: event.tag,
      tag_selected: event.tagSelected,
      tags_count: event.tagsCount,
      has_comment: event.hasComment,
    }, {
      feedback_comment_length: event.commentLength ?? 0,
    });
  }

  onClosedAfterSubmit(): void {
    this.exerciseSessionTracking.trackEvent('feedback_modal_closed_after_submit');
    this.closeModal();
    this.continuePendingLeaveFlow(true);
  }

  onFindAnotherExercise(navigateToExercises: () => void): void {
    this.exerciseSessionTracking.trackEvent('feedback_modal_find_another_exercise_clicked');
    this.closeModal();
    this.continuePendingLeaveFlow(false);
    navigateToExercises();
  }

  private shouldPromptFeedbackOnLeave(exerciseState: models.ExerciseState): boolean {
    return exerciseState === 'results' && this.feedbackService.shouldShowPrompt();
  }

  private openModalIfEligible(): boolean {
    if (this.isModalOpen()) {
      return true;
    }

    if (!this.feedbackService.consumePromptOpportunity()) {
      return false;
    }

    this.exerciseSessionTracking.trackEvent('feedback_modal_opened');
    this.isModalOpen.set(true);
    return true;
  }

  private closeModal(): void {
    this.isModalOpen.set(false);
  }

  private continuePendingLeaveFlow(allowPendingAction: boolean): void {
    const routeResolver = this.pendingRouteLeaveResolver;
    this.pendingRouteLeaveResolver = null;

    if (routeResolver) {
      routeResolver(allowPendingAction);
      this.pendingLeaveAction = null;
      return;
    }

    const leaveAction = this.pendingLeaveAction;
    this.pendingLeaveAction = null;

    if (allowPendingAction) {
      leaveAction?.();
    }
  }
}

import { TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { ExerciseFeedbackFlowService } from './exercise-feedback-flow.service';
import { FeedbackService } from './feedback.service';

describe('ExerciseFeedbackFlowService', () => {
  let service: ExerciseFeedbackFlowService;
  let authSessionStoreMock: jasmine.SpyObj<AuthSessionStore>;
  let trackingMock: jasmine.SpyObj<ExerciseSessionTrackingService>;
  let feedbackServiceMock: jasmine.SpyObj<FeedbackService>;

  beforeEach(() => {
    authSessionStoreMock = jasmine.createSpyObj<AuthSessionStore>(
      'AuthSessionStore',
      ['userId'],
    );
    trackingMock = jasmine.createSpyObj<ExerciseSessionTrackingService>(
      'ExerciseSessionTrackingService',
      ['trackEvent', 'getCurrentSessionId'],
    );
    feedbackServiceMock = jasmine.createSpyObj<FeedbackService>(
      'FeedbackService',
      ['shouldShowPrompt', 'consumePromptOpportunity', 'markDismissed', 'submitFeedback'],
    );

    authSessionStoreMock.userId.and.returnValue('user-123');
    trackingMock.getCurrentSessionId.and.returnValue('session-123');
    feedbackServiceMock.shouldShowPrompt.and.returnValue(false);
    feedbackServiceMock.consumePromptOpportunity.and.returnValue(false);

    TestBed.configureTestingModule({
      providers: [
        ExerciseFeedbackFlowService,
        {
          provide: AuthSessionStore,
          useValue: authSessionStoreMock,
        },
        {
          provide: ExerciseSessionTrackingService,
          useValue: trackingMock,
        },
        {
          provide: FeedbackService,
          useValue: feedbackServiceMock,
        },
      ],
    });

    service = TestBed.inject(ExerciseFeedbackFlowService);
  });

  it('should allow route deactivation when feedback prompt is not needed', () => {
    expect(service.canDeactivateFromRoute('intro')).toBeTrue();
    expect(service.canDeactivateFromRoute('results')).toBeTrue();
    expect(service.isModalOpen()).toBeFalse();
  });

  it('should open the feedback modal and resolve route leave after dismissal', fakeAsync(() => {
    feedbackServiceMock.shouldShowPrompt.and.returnValue(true);
    feedbackServiceMock.consumePromptOpportunity.and.returnValue(true);
    let routeAllowed: boolean | null = null;

    const result = service.canDeactivateFromRoute('results');
    expect(result).toEqual(jasmine.any(Promise));
    expect(service.isModalOpen()).toBeTrue();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith('feedback_modal_opened');

    (result as Promise<boolean>).then((allowed) => {
      routeAllowed = allowed;
    });
    service.onDismissed('not_now');
    flushMicrotasks();

    expect(routeAllowed).toBeTrue();
    expect(service.isModalOpen()).toBeFalse();
    expect(feedbackServiceMock.markDismissed).toHaveBeenCalled();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'feedback_modal_dismissed',
      jasmine.objectContaining({
        reason: 'not_now',
      }),
    );
  }));

  it('should run pending leave actions after the modal is closed', () => {
    feedbackServiceMock.shouldShowPrompt.and.returnValue(true);
    feedbackServiceMock.consumePromptOpportunity.and.returnValue(true);
    const action = jasmine.createSpy('action');

    service.attemptLeaveResults('results', action);

    expect(action).not.toHaveBeenCalled();
    expect(service.isModalOpen()).toBeTrue();

    service.onClosedAfterSubmit();

    expect(action).toHaveBeenCalled();
    expect(service.isModalOpen()).toBeFalse();
  });

  it('should skip pending leave action and navigate when finding another exercise from the modal', () => {
    feedbackServiceMock.shouldShowPrompt.and.returnValue(true);
    feedbackServiceMock.consumePromptOpportunity.and.returnValue(true);
    const pendingAction = jasmine.createSpy('pendingAction');
    const navigateToExercises = jasmine.createSpy('navigateToExercises');

    service.attemptLeaveResults('results', pendingAction);
    service.onFindAnotherExercise(navigateToExercises);

    expect(pendingAction).not.toHaveBeenCalled();
    expect(navigateToExercises).toHaveBeenCalled();
    expect(service.isModalOpen()).toBeFalse();
  });

  it('should resolve route leave as false when finding another exercise from the modal', fakeAsync(() => {
    feedbackServiceMock.shouldShowPrompt.and.returnValue(true);
    feedbackServiceMock.consumePromptOpportunity.and.returnValue(true);
    const navigateToExercises = jasmine.createSpy('navigateToExercises');
    let routeAllowed: boolean | null = null;

    const result = service.canDeactivateFromRoute('results') as Promise<boolean>;
    result.then((allowed) => {
      routeAllowed = allowed;
    });

    service.onFindAnotherExercise(navigateToExercises);
    flushMicrotasks();

    expect(routeAllowed).toBeFalse();
    expect(navigateToExercises).toHaveBeenCalled();
  }));

  it('should submit feedback with exercise, session, and user context', () => {
    service.onSubmitted({
      rating: 5,
      tags: ['useful'],
      comment: 'Great exercise',
    }, {
      exerciseId: 42,
      proposition: {
        id: 42,
        complexityId: 'Beginner',
        subjectId: 'Business',
      } as any,
    });

    expect(feedbackServiceMock.submitFeedback).toHaveBeenCalledWith(jasmine.objectContaining({
      rating: 5,
      tags: ['useful'],
      comment: 'Great exercise',
      exerciseId: '42',
      difficulty: 'Beginner',
      topic: 'Business',
      sessionId: 'session-123',
      userId: 'user-123',
    }));
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'feedback_modal_submitted',
      jasmine.objectContaining({
        rating: 5,
        tags_count: 1,
        has_comment: true,
      }),
      jasmine.objectContaining({
        feedback_rating: 5,
        feedback_tags_count: 1,
        feedback_comment_length: 14,
      }),
    );
  });

  it('should allow pending route leave on destroy', fakeAsync(() => {
    feedbackServiceMock.shouldShowPrompt.and.returnValue(true);
    feedbackServiceMock.consumePromptOpportunity.and.returnValue(true);
    let routeAllowed: boolean | null = null;

    const result = service.canDeactivateFromRoute('results') as Promise<boolean>;
    result.then((allowed) => {
      routeAllowed = allowed;
    });

    service.destroy();
    flushMicrotasks();

    expect(routeAllowed).toBeTrue();
  }));
});

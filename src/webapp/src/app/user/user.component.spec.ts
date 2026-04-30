import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FeedbackPromptStatusResponse } from '../auth/models/feedback-prompt.model';
import { AuthApiService } from '../auth/services/auth-api.service';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { UserProgressApiService } from './services/user-progress-api.service';
import { UserComponent } from './user.component';
import { Insights } from '../../telemetry/insights.service';

describe('UserComponent', () => {
  let component: UserComponent;
  let fixture: ComponentFixture<UserComponent>;
  let authApiSpy: jasmine.SpyObj<AuthApiService>;
  let userProgressApiSpy: jasmine.SpyObj<UserProgressApiService>;
  let insightsSpy: jasmine.SpyObj<Insights>;
  let router: Router;
  let authSessionStoreMock: Pick<AuthSessionStore, 'state' | 'invalidateSession'>;

  beforeEach(async () => {
    authApiSpy = jasmine.createSpyObj<AuthApiService>('AuthApiService', [
      'feedbackPromptStatus',
      'markFeedbackPromptShown',
      'markFeedbackPromptDismissed',
      'markFeedbackPromptSubmitted',
    ]);
    userProgressApiSpy = jasmine.createSpyObj<UserProgressApiService>('UserProgressApiService', ['summary', 'items']);
    insightsSpy = jasmine.createSpyObj<Insights>('Insights', ['trackException', 'trackEvent']);
    authSessionStoreMock = {
      state: signal({
        isAuthenticated: true,
        userId: 'user-123',
        email: 'user@test.com',
        emailConfirmed: true,
        listenWriteTutorialCompleted: true,
        hasReliableSessionState: true,
        issuedAtUtc: new Date().toISOString(),
        expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        isLoading: false,
        error: null,
      }),
      invalidateSession: jasmine.createSpy('invalidateSession'),
    };

    authApiSpy.feedbackPromptStatus.and.returnValue(of(createFeedbackPromptStatus(false)));
    authApiSpy.markFeedbackPromptShown.and.returnValue(of(createFeedbackPromptStatus(true)));
    authApiSpy.markFeedbackPromptDismissed.and.returnValue(of(createFeedbackPromptStatus(false)));
    authApiSpy.markFeedbackPromptSubmitted.and.returnValue(of(createFeedbackPromptStatus(false)));
    userProgressApiSpy.summary.and.returnValue(of({
      trackingEnabled: true,
      totalItems: 2,
      inProgressCount: 1,
      completedCount: 1,
      totalAttempts: 3,
      totalActiveSeconds: 3665,
      averageAccuracyPercentage: 0.75,
      bestAccuracyPercentage: 0.9,
      lastActivityAtUtc: new Date().toISOString(),
    }));

    userProgressApiSpy.items.and.returnValue(of([
      {
        exerciseId: 10,
        status: 'completed',
        exerciseTitle: 'Exercise 10',
        subject: 'World',
        complexity: 'Medium',
        attemptCount: 2,
        latestAccuracyPercentage: 0.8,
        bestAccuracyPercentage: 0.9,
        activeSeconds: 420,
        startedAtUtc: new Date().toISOString(),
        completedAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
        currentWordCount: 140,
        originalWordCount: 150,
      },
      {
        exerciseId: 11,
        status: 'in_progress',
        exerciseTitle: 'Exercise 11',
        subject: 'Tech',
        complexity: 'Hard',
        attemptCount: 1,
        latestAccuracyPercentage: null,
        bestAccuracyPercentage: null,
        activeSeconds: 95,
        startedAtUtc: new Date().toISOString(),
        completedAtUtc: null,
        updatedAtUtc: new Date().toISOString(),
        currentWordCount: 24,
        originalWordCount: 120,
      },
    ]));

    await TestBed.configureTestingModule({
      imports: [UserComponent],
      providers: [
        {
          provide: AuthSessionStore,
          useValue: authSessionStoreMock,
        },
        { provide: UserProgressApiService, useValue: userProgressApiSpy },
        { provide: AuthApiService, useValue: authApiSpy },
        { provide: Insights, useValue: insightsSpy },
        provideRouter([]),
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(UserComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render in-progress and completed statuses', () => {
    const root: HTMLElement = fixture.nativeElement;

    expect(root.textContent).toContain('Completed');
    expect(root.textContent).toContain('In progress');
    expect(root.textContent).toContain('user@test.com');
    expect(root.textContent).toContain('Total active time');
    expect(root.textContent).toContain('01:01:05');
    expect(root.textContent).toContain('Active so far');
    expect(root.textContent).toContain('Active time');
    expect(root.textContent).toContain('Words');
    expect(root.textContent).toContain('140/150');
    expect(root.textContent).toContain('24/120');
    expect(userProgressApiSpy.summary).toHaveBeenCalled();
    expect(userProgressApiSpy.items).toHaveBeenCalled();
  });

  it('should not request progress feedback prompt before 3 completed exercises', () => {
    expect(authApiSpy.feedbackPromptStatus).not.toHaveBeenCalled();
    expect(component.isProgressFeedbackModalOpen()).toBeFalse();
  });

  it('should show progress feedback prompt when eligible after 3 completed exercises', async () => {
    const eligibleStatus = createFeedbackPromptStatus(true);
    authApiSpy.feedbackPromptStatus.and.returnValue(of(eligibleStatus));
    authApiSpy.markFeedbackPromptShown.and.returnValue(of(eligibleStatus));
    userProgressApiSpy.summary.and.returnValue(of(createProgressSummary({ completedCount: 3, totalAttempts: 6 })));
    authApiSpy.feedbackPromptStatus.calls.reset();
    authApiSpy.markFeedbackPromptShown.calls.reset();
    insightsSpy.trackEvent.calls.reset();

    await component.reload();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(authApiSpy.feedbackPromptStatus).toHaveBeenCalledWith('progress_feedback_v1');
    expect(authApiSpy.markFeedbackPromptShown).toHaveBeenCalledWith('progress_feedback_v1');
    expect(component.isProgressFeedbackModalOpen()).toBeTrue();
    expect(insightsSpy.trackEvent).toHaveBeenCalledWith(
      'progress_feedback_modal_opened',
      jasmine.objectContaining({
        area: 'user_progress',
        campaign_key: 'progress_feedback_v1',
        user_id: 'user-123',
        prompt_is_eligible: 'true',
      }),
      jasmine.objectContaining({
        completed_count: 3,
        total_attempts: 6,
      }),
    );
  });

  it('should submit progress feedback to telemetry and mark prompt submitted', () => {
    component.summary.set(createProgressSummary({ completedCount: 4, totalAttempts: 8 }));
    component.isProgressFeedbackModalOpen.set(true);
    authApiSpy.markFeedbackPromptSubmitted.calls.reset();
    insightsSpy.trackEvent.calls.reset();

    component.onProgressFeedbackSubmitted('  The progress page is useful.  ');

    expect(component.isProgressFeedbackModalOpen()).toBeFalse();
    expect(authApiSpy.markFeedbackPromptSubmitted).toHaveBeenCalledWith('progress_feedback_v1');
    expect(insightsSpy.trackEvent).toHaveBeenCalledWith(
      'progress_feedback_submitted',
      jasmine.objectContaining({
        area: 'user_progress',
        campaign_key: 'progress_feedback_v1',
        user_id: 'user-123',
        comment: 'The progress page is useful.',
      }),
      jasmine.objectContaining({
        completed_count: 4,
        total_attempts: 8,
        comment_length: 28,
      }),
    );
  });

  it('should mark progress feedback dismissed when user closes the modal', () => {
    component.summary.set(createProgressSummary({ completedCount: 3, totalAttempts: 5 }));
    component.isProgressFeedbackModalOpen.set(true);
    authApiSpy.markFeedbackPromptDismissed.calls.reset();
    insightsSpy.trackEvent.calls.reset();

    component.onProgressFeedbackDismissed('not_now');

    expect(component.isProgressFeedbackModalOpen()).toBeFalse();
    expect(authApiSpy.markFeedbackPromptDismissed).toHaveBeenCalledWith('progress_feedback_v1');
    expect(insightsSpy.trackEvent).toHaveBeenCalledWith(
      'progress_feedback_modal_dismissed',
      jasmine.objectContaining({
        area: 'user_progress',
        campaign_key: 'progress_feedback_v1',
        user_id: 'user-123',
        dismiss_reason: 'not_now',
      }),
      jasmine.objectContaining({
        completed_count: 3,
        total_attempts: 5,
      }),
    );
  });

  it('should render tracker card links for valid exercise ids', () => {
    const root: HTMLElement = fixture.nativeElement;
    const links = Array.from(root.querySelectorAll<HTMLAnchorElement>('.tracker-item-link'));

    expect(links.length).toBe(2);
    expect(links[0].getAttribute('href')).toContain('/english-writing-exercise/10');
    expect(links[1].getAttribute('href')).toContain('/english-writing-exercise/11');
  });

  it('should render non-link tracker card body when exercise id is invalid', async () => {
    userProgressApiSpy.items.and.returnValue(of([
      {
        exerciseId: 0,
        status: 'in_progress',
        exerciseTitle: 'Broken Exercise',
        subject: 'General',
        complexity: 'Easy',
        attemptCount: 1,
        latestAccuracyPercentage: 0.7,
        bestAccuracyPercentage: 0.8,
        activeSeconds: 20,
        startedAtUtc: new Date().toISOString(),
        completedAtUtc: null,
        updatedAtUtc: new Date().toISOString(),
        currentWordCount: 10,
        originalWordCount: 20,
      },
    ]));

    await component.reload();
    fixture.detectChanges();

    const root: HTMLElement = fixture.nativeElement;

    expect(root.querySelector('.tracker-item-link')).toBeNull();
    expect(root.querySelector('.tracker-item-body[aria-disabled="true"]')).not.toBeNull();
  });

  it('should track exception when progress load fails', async () => {
    const timeoutError = new Error('Timed out');
    timeoutError.name = 'TimeoutError';
    userProgressApiSpy.summary.and.returnValue(throwError(() => timeoutError));

    await component.reload();

    expect(component.error()).toBe('Could not load progress right now. Please try again.');
    expect(insightsSpy.trackException).toHaveBeenCalledWith(timeoutError, {
      properties: jasmine.objectContaining({
        area: 'user_progress',
        operation: 'load_user_progress',
        error_kind: 'timeout',
        http_status: 'unknown',
      }),
      measurements: jasmine.objectContaining({
        http_status: 0,
      }),
    });
  });

  it('should invalidate session and redirect to login on 401', async () => {
    const unauthorized = new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' });
    userProgressApiSpy.summary.and.returnValue(throwError(() => unauthorized));

    await component.reload();

    expect(authSessionStoreMock.invalidateSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: {
        returnUrl: '/user',
        source: 'user_progress_session_expired',
      },
    });
    expect(component.error()).toBeNull();
    expect(insightsSpy.trackEvent).toHaveBeenCalledWith(
      'user_progress_session_expired',
      jasmine.objectContaining({
        area: 'user_progress',
        operation: 'load_user_progress',
        error_kind: 'session_expired',
        http_status: '401',
      }),
      jasmine.objectContaining({
        http_status: 401,
      }),
    );
    expect(insightsSpy.trackException).not.toHaveBeenCalledWith(unauthorized, jasmine.anything());
  });

  it('should redirect to login without session-expired telemetry when 401 happens without a local session', async () => {
    (authSessionStoreMock.state as any).set({
      ...authSessionStoreMock.state(),
      isAuthenticated: false,
      userId: null,
      email: null,
    });
    const unauthorized = new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' });
    userProgressApiSpy.summary.and.returnValue(throwError(() => unauthorized));

    await component.reload();

    expect(authSessionStoreMock.invalidateSession).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: {
        returnUrl: '/user',
        source: 'user_progress_unauthorized',
      },
    });
    expect(insightsSpy.trackEvent).toHaveBeenCalledWith(
      'user_progress_unauthorized',
      jasmine.objectContaining({
        area: 'user_progress',
        operation: 'load_user_progress',
        error_kind: 'missing_session',
        http_status: '401',
      }),
      jasmine.objectContaining({
        http_status: 401,
      }),
    );
    expect(insightsSpy.trackException).not.toHaveBeenCalledWith(unauthorized, jasmine.anything());
  });
});

function createProgressSummary(overrides: Partial<{
  trackingEnabled: boolean;
  totalItems: number;
  inProgressCount: number;
  completedCount: number;
  totalAttempts: number;
  totalActiveSeconds: number;
  averageAccuracyPercentage: number | null;
  bestAccuracyPercentage: number | null;
  lastActivityAtUtc: string | null;
}> = {}) {
  return {
    trackingEnabled: true,
    totalItems: 4,
    inProgressCount: 1,
    completedCount: 1,
    totalAttempts: 3,
    totalActiveSeconds: 3665,
    averageAccuracyPercentage: 0.75,
    bestAccuracyPercentage: 0.9,
    lastActivityAtUtc: new Date().toISOString(),
    ...overrides,
  };
}

function createFeedbackPromptStatus(isEligible: boolean): FeedbackPromptStatusResponse {
  return {
    campaignKey: 'progress_feedback_v1',
    isEligible,
    nextEligibleAtUtc: isEligible ? null : new Date(Date.now() + 21 * 24 * 60 * 60 * 1000).toISOString(),
    lastShownAtUtc: null,
    lastDismissedAtUtc: null,
    lastSubmittedAtUtc: null,
    dismissCount: 0,
    submitCount: 0,
  };
}

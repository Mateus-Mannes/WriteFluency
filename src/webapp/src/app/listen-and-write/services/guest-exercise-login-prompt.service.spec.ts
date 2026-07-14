import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { BrowserService } from '../../core/services/browser.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { GuestExerciseLoginPromptService } from './guest-exercise-login-prompt.service';
import * as constants from '../listen-and-write.constants';
import * as models from '../listen-and-write.models';

describe('GuestExerciseLoginPromptService', () => {
  let service: GuestExerciseLoginPromptService;
  let authSessionStoreMock: jasmine.SpyObj<AuthSessionStore>;
  let browserServiceMock: jasmine.SpyObj<BrowserService>;
  let trackingMock: jasmine.SpyObj<ExerciseSessionTrackingService>;
  let routerMock: jasmine.SpyObj<Router>;
  let storage: Record<string, string>;

  const beginRequest = {
    isFirstTimeUser: false,
    audioEndedBeforeBegin: false,
  };

  beforeEach(() => {
    storage = {};
    authSessionStoreMock = jasmine.createSpyObj<AuthSessionStore>(
      'AuthSessionStore',
      ['isAuthenticated'],
    );
    browserServiceMock = jasmine.createSpyObj<BrowserService>(
      'BrowserService',
      ['getItem', 'setItem'],
    );
    trackingMock = jasmine.createSpyObj<ExerciseSessionTrackingService>(
      'ExerciseSessionTrackingService',
      ['trackEvent'],
    );
    routerMock = jasmine.createSpyObj<Router>(
      'Router',
      ['navigate'],
    );

    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    browserServiceMock.getItem.and.callFake((key: string) => storage[key] ?? null);
    browserServiceMock.setItem.and.callFake((key: string, value: string) => {
      storage[key] = value;
    });
    routerMock.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      providers: [
        GuestExerciseLoginPromptService,
        {
          provide: AuthSessionStore,
          useValue: authSessionStoreMock,
        },
        {
          provide: BrowserService,
          useValue: browserServiceMock,
        },
        {
          provide: ExerciseSessionTrackingService,
          useValue: trackingMock,
        },
        {
          provide: Router,
          useValue: routerMock,
        },
      ],
    });

    service = TestBed.inject(GuestExerciseLoginPromptService);
  });

  it('should increment guest attempts and skip the modal before the threshold', () => {
    const result = service.prepareBeginExercise(beginRequest);

    expect(result.context.guestBeginAttemptCount).toBe(1);
    expect(result.decision).toEqual({
      shouldShow: false,
      reason: 'below_threshold',
      cooldownRemainingMs: 0,
    });
    expect(storage[constants.guestBeginAttemptCountStorageKey]).toBe('1');
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'guest_login_modal_not_shown',
      jasmine.objectContaining({
        reason: 'below_threshold',
        guest_begin_attempt_count: 1,
      }),
      jasmine.objectContaining({
        guest_begin_attempt_count: 1,
      }),
    );
  });

  it('should open the modal on an eligible guest attempt', () => {
    storage[constants.guestBeginAttemptCountStorageKey] = '1';

    const result = service.prepareBeginExercise(beginRequest);
    service.open(result.context);

    expect(result.context.guestBeginAttemptCount).toBe(2);
    expect(result.decision.reason).toBe('eligible');
    expect(service.isOpen()).toBeTrue();
    expect(storage[constants.guestBeginLoginModalLastShownStorageKey]).toBeTruthy();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'guest_login_modal_shown',
      jasmine.objectContaining({
        source: 'begin_exercise',
        guest_begin_attempt_count: 2,
      }),
      jasmine.objectContaining({
        guest_begin_attempt_count: 2,
      }),
    );
  });

  it('should respect the modal cooldown', () => {
    storage[constants.guestBeginAttemptCountStorageKey] = '4';
    storage[constants.guestBeginLoginModalLastShownStorageKey] = new Date().toISOString();

    const result = service.prepareBeginExercise(beginRequest);

    expect(result.context.guestBeginAttemptCount).toBe(5);
    expect(result.decision.shouldShow).toBeFalse();
    expect(result.decision.reason).toBe('cooldown_active');
    expect(result.decision.cooldownRemainingMs).toBeGreaterThan(0);
  });

  it('should bypass guest modal decisions for authenticated users', () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);

    const result = service.prepareBeginExercise(beginRequest);

    expect(result.context.guestBeginAttemptCount).toBeNull();
    expect(result.decision).toEqual({
      shouldShow: false,
      reason: 'authenticated',
      cooldownRemainingMs: 0,
    });
    expect(browserServiceMock.setItem).not.toHaveBeenCalledWith(
      constants.guestBeginAttemptCountStorageKey,
      jasmine.any(String),
    );
  });

  it('should close the modal and return a start context when continuing as guest', () => {
    const beginContext: models.BeginExerciseContext = {
      isFirstTimeUser: false,
      audioEndedBeforeBegin: true,
      guestBeginAttemptCount: 2,
      guestLoginModalShownBeforeStart: false,
    };

    service.open(beginContext);

    const result = service.continueAsGuest();

    expect(service.isOpen()).toBeFalse();
    expect(result).toEqual({
      ...beginContext,
      guestLoginModalShownBeforeStart: true,
    });
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'guest_login_modal_dismissed',
      jasmine.objectContaining({
        reason: 'continue_as_guest',
        source: 'begin_exercise',
      }),
      jasmine.objectContaining({
        guest_begin_attempt_count: 2,
      }),
    );
  });

  it('should close the modal and navigate to login from the sign-in action', () => {
    const beginContext: models.BeginExerciseContext = {
      isFirstTimeUser: false,
      audioEndedBeforeBegin: false,
      guestBeginAttemptCount: 2,
      guestLoginModalShownBeforeStart: false,
    };

    service.open(beginContext);
    service.signIn('/english-writing-exercise/42');

    expect(service.isOpen()).toBeFalse();
    expect(routerMock.navigate).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: {
        returnUrl: '/english-writing-exercise/42',
        source: 'begin_exercise_modal',
      },
    });
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'guest_login_modal_login_clicked',
      jasmine.objectContaining({
        source: 'begin_exercise',
        return_url: '/english-writing-exercise/42',
      }),
      jasmine.objectContaining({
        guest_begin_attempt_count: 2,
      }),
    );
  });
});

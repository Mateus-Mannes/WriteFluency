import { HttpErrorResponse } from '@angular/common/http';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { UserProgressApiService } from '../../user/services/user-progress-api.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { ExerciseProgressTrackingService } from './exercise-progress-tracking.service';
import { Insights } from '../../../telemetry/insights.service';

describe('ExerciseProgressTrackingService', () => {
  let service: ExerciseProgressTrackingService;
  let authSessionStoreMock: jasmine.SpyObj<AuthSessionStore>;
  let userProgressApiMock: jasmine.SpyObj<UserProgressApiService>;
  let exerciseSessionTrackingMock: jasmine.SpyObj<ExerciseSessionTrackingService>;
  let insightsMock: jasmine.SpyObj<Insights>;

  beforeEach(() => {
    authSessionStoreMock = jasmine.createSpyObj<AuthSessionStore>(
      'AuthSessionStore',
      ['isAuthenticated', 'invalidateSession'],
    );
    userProgressApiMock = jasmine.createSpyObj<UserProgressApiService>(
      'UserProgressApiService',
      ['start', 'complete', 'saveState', 'state'],
    );
    exerciseSessionTrackingMock = jasmine.createSpyObj<ExerciseSessionTrackingService>(
      'ExerciseSessionTrackingService',
      ['trackEvent', 'hasActiveSession', 'getCurrentSessionId', 'getCurrentOperationId'],
    );
    insightsMock = jasmine.createSpyObj<Insights>('Insights', ['trackEvent', 'trackException']);

    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    exerciseSessionTrackingMock.hasActiveSession.and.returnValue(true);
    exerciseSessionTrackingMock.getCurrentSessionId.and.returnValue('session-123');
    exerciseSessionTrackingMock.getCurrentOperationId.and.returnValue('operation-456');
    userProgressApiMock.start.and.returnValue(of({
      trackingEnabled: true,
      exerciseId: 1,
      status: 'in_progress',
      updatedAtUtc: new Date().toISOString(),
    }));
    userProgressApiMock.complete.and.returnValue(of({
      trackingEnabled: true,
      exerciseId: 1,
      status: 'completed',
      updatedAtUtc: new Date().toISOString(),
    }));
    userProgressApiMock.saveState.and.returnValue(of({
      trackingEnabled: true,
      exerciseId: 1,
      status: 'in_progress',
      updatedAtUtc: new Date().toISOString(),
    }));
    userProgressApiMock.state.and.returnValue(of({
      trackingEnabled: true,
      exerciseId: 1,
      hasServerState: true,
      exerciseState: 'exercise',
      userText: 'cached text',
      wordCount: 2,
      autoPauseSeconds: 3,
      pausedTimeSeconds: 4,
      updatedAtUtc: new Date().toISOString(),
    }));

    TestBed.configureTestingModule({
      providers: [
        ExerciseProgressTrackingService,
        { provide: AuthSessionStore, useValue: authSessionStoreMock },
        { provide: UserProgressApiService, useValue: userProgressApiMock },
        { provide: ExerciseSessionTrackingService, useValue: exerciseSessionTrackingMock },
        { provide: Insights, useValue: insightsMock },
      ],
    });

    service = TestBed.inject(ExerciseProgressTrackingService);
  });

  it('should not call progress APIs when user is not authenticated', fakeAsync(() => {
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    let loadedState: unknown = 'pending';

    service.trackStart({ id: 5, title: 'Exercise 5' } as any);
    service.trackComplete({ id: 5, title: 'Exercise 5' } as any, null as any);
    service.saveState({ id: 5, title: 'Exercise 5' } as any, {
      exerciseState: 'exercise',
      userText: 'one two',
      autoPauseSeconds: 2,
      pausedTimeSeconds: 3,
    });
    tick(2500);
    service.loadState(5).then((state) => {
      loadedState = state;
    });
    tick();

    expect(userProgressApiMock.start).not.toHaveBeenCalled();
    expect(userProgressApiMock.complete).not.toHaveBeenCalled();
    expect(userProgressApiMock.saveState).not.toHaveBeenCalled();
    expect(userProgressApiMock.state).not.toHaveBeenCalled();
    expect(loadedState).toBeNull();
  }));

  it('should handle 401 by invalidating auth state and showing session-expired notification', () => {
    userProgressApiMock.start.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })));

    service.trackStart({ id: 5, title: 'Exercise 5' } as any);

    expect(authSessionStoreMock.invalidateSession).toHaveBeenCalled();
    expect(service.syncNotification()).toEqual(jasmine.objectContaining({
      kind: 'session_expired',
    }));
    expect(exerciseSessionTrackingMock.trackEvent).toHaveBeenCalledWith(
      'exercise_progress_sync_failure',
      jasmine.objectContaining({
        operation: 'start',
        failure_kind: 'unauthorized',
        http_status: '401',
        exercise_id: '5',
      }),
      jasmine.objectContaining({
        http_status: 401,
      }),
    );
    expect(insightsMock.trackException).toHaveBeenCalledWith(
      jasmine.any(Error),
      jasmine.objectContaining({
        properties: jasmine.objectContaining({
          operation: 'start',
          failure_kind: 'unauthorized',
          notification_kind: 'session_expired',
          exercise_id: '5',
          wf_session_id: 'session-123',
          wf_operation_id: 'operation-456',
        }),
        measurements: jasmine.objectContaining({
          http_status: 401,
        }),
      }),
    );
  });

  it('should keep session-expired notification visible until user dismisses it', fakeAsync(() => {
    userProgressApiMock.start.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })));

    service.trackStart({ id: 5, title: 'Exercise 5' } as any);
    tick(11000);

    expect(service.syncNotification()).toEqual(jasmine.objectContaining({
      kind: 'session_expired',
    }));

    service.dismissSyncNotification();
    expect(service.syncNotification()).toBeNull();
  }));

  it('should handle non-401 errors with warning notification and telemetry', () => {
    userProgressApiMock.complete.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 500, statusText: 'Server Error' })));

    service.trackComplete({ id: 5, title: 'Exercise 5' } as any, {
      accuracyPercentage: 0.9,
      userText: 'one two',
      originalText: 'one two three',
    } as any);

    expect(authSessionStoreMock.invalidateSession).not.toHaveBeenCalled();
    expect(service.syncNotification()).toEqual(jasmine.objectContaining({
      kind: 'warning',
    }));
    expect(exerciseSessionTrackingMock.trackEvent).toHaveBeenCalledWith(
      'exercise_progress_sync_failure',
      jasmine.objectContaining({
        operation: 'complete',
        failure_kind: 'api_error',
        http_status: '500',
        exercise_id: '5',
      }),
      jasmine.objectContaining({
        http_status: 500,
      }),
    );
    expect(insightsMock.trackException).toHaveBeenCalledWith(
      jasmine.any(Error),
      jasmine.objectContaining({
        properties: jasmine.objectContaining({
          operation: 'complete',
          failure_kind: 'api_error',
          notification_kind: 'warning',
          exercise_id: '5',
        }),
        measurements: jasmine.objectContaining({
          http_status: 500,
        }),
      }),
    );
  });

  it('should show warning notification only once per try and reset after new start', () => {
    userProgressApiMock.complete.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 503, statusText: 'Service Unavailable' })));

    service.trackStart({ id: 5, title: 'Exercise 5' } as any);

    service.trackComplete({ id: 5, title: 'Exercise 5' } as any, null as any);
    expect(service.syncNotification()).toEqual(jasmine.objectContaining({ kind: 'warning' }));
    expect(insightsMock.trackException).toHaveBeenCalledTimes(1);

    service.dismissSyncNotification();
    expect(service.syncNotification()).toBeNull();

    service.trackComplete({ id: 5, title: 'Exercise 5' } as any, null as any);
    expect(service.syncNotification()).toBeNull();
    expect(insightsMock.trackException).toHaveBeenCalledTimes(1);

    userProgressApiMock.start.and.returnValue(of({
      trackingEnabled: true,
      exerciseId: 5,
      status: 'in_progress',
      updatedAtUtc: new Date().toISOString(),
    }));
    service.trackStart({ id: 5, title: 'Exercise 5' } as any);
    service.trackComplete({ id: 5, title: 'Exercise 5' } as any, null as any);

    expect(service.syncNotification()).toEqual(jasmine.objectContaining({ kind: 'warning' }));
    expect(insightsMock.trackException).toHaveBeenCalledTimes(2);
  });

  it('should log to Insights directly when there is no active exercise session', () => {
    exerciseSessionTrackingMock.hasActiveSession.and.returnValue(false);
    userProgressApiMock.complete.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 500, statusText: 'Server Error' })));

    service.trackComplete({ id: 9, title: 'Exercise 9' } as any, null as any);

    expect(exerciseSessionTrackingMock.trackEvent).not.toHaveBeenCalled();
    expect(insightsMock.trackEvent).toHaveBeenCalledWith(
      'exercise_progress_sync_failure',
      jasmine.objectContaining({
        operation: 'complete',
        failure_kind: 'api_error',
        http_status: '500',
        exercise_id: '9',
      }),
      jasmine.objectContaining({
        http_status: 500,
      }),
    );
  });

  it('should debounce save-state calls and send latest snapshot', fakeAsync(() => {
    service.saveState(
      { id: 5, title: 'Exercise 5' } as any,
      {
        exerciseState: 'exercise',
        userText: 'one two',
        autoPauseSeconds: 2,
        pausedTimeSeconds: 3,
      },
    );

    service.saveState(
      { id: 5, title: 'Exercise 5' } as any,
      {
        exerciseState: 'exercise',
        userText: 'one two three',
        autoPauseSeconds: 3,
        pausedTimeSeconds: 4,
      },
    );

    tick(1999);
    expect(userProgressApiMock.saveState).not.toHaveBeenCalled();

    tick(1);
    expect(userProgressApiMock.saveState).toHaveBeenCalledTimes(1);
    expect(userProgressApiMock.saveState).toHaveBeenCalledWith({
      exerciseId: 5,
      exerciseState: 'exercise',
      userText: 'one two three',
      wordCount: 3,
      originalWordCount: 0,
      autoPauseSeconds: 3,
      pausedTimeSeconds: 4,
      exerciseTitle: 'Exercise 5',
      subject: null,
      complexity: null,
    });
  }));

  it('should fallback to subjectId and complexityId when description metadata is missing', () => {
    service.trackStart({
      id: 12,
      title: 'Exercise 12',
      subjectId: 'Sports',
      complexityId: 'Advanced',
      subject: null,
      complexity: null,
    } as any);

    expect(userProgressApiMock.start).toHaveBeenCalledWith({
      exerciseId: 12,
      exerciseTitle: 'Exercise 12',
      subject: 'Sports',
      complexity: 'Advanced',
      originalWordCount: 0,
    });
  });
});

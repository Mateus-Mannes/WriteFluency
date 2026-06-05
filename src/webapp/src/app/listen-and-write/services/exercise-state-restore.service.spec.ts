import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { BrowserService } from '../../core/services/browser.service';
import { ExerciseProgressTrackingService } from './exercise-progress-tracking.service';
import { ExerciseStateRestoreService } from './exercise-state-restore.service';
import * as models from '../listen-and-write.models';

describe('ExerciseStateRestoreService', () => {
  let service: ExerciseStateRestoreService;
  let authSessionStoreMock: jasmine.SpyObj<AuthSessionStore>;
  let browserServiceMock: jasmine.SpyObj<BrowserService>;
  let progressTrackingMock: jasmine.SpyObj<ExerciseProgressTrackingService>;
  let appliedSnapshots: models.RestoredExerciseSnapshot[];

  const createRestoreRequest = (exerciseId: number | null) => ({
    exerciseId,
    getStateKey: (id: number) => `listen-write-state-${id}`,
    getExerciseStateKey: (id: number) => `exercise-section-state-${id}`,
    applySnapshot: (snapshot: models.RestoredExerciseSnapshot) => {
      appliedSnapshots.push(snapshot);
    },
    resetPendingPausedTime: jasmine.createSpy('resetPendingPausedTime'),
  });

  beforeEach(() => {
    authSessionStoreMock = jasmine.createSpyObj<AuthSessionStore>(
      'AuthSessionStore',
      ['isAuthenticated'],
    );
    browserServiceMock = jasmine.createSpyObj<BrowserService>(
      'BrowserService',
      ['isBrowserEnvironment', 'getItem', 'removeItem'],
    );
    progressTrackingMock = jasmine.createSpyObj<ExerciseProgressTrackingService>(
      'ExerciseProgressTrackingService',
      ['loadState', 'trackComplete'],
    );
    appliedSnapshots = [];

    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    browserServiceMock.isBrowserEnvironment.and.returnValue(true);
    browserServiceMock.getItem.and.returnValue(null);
    progressTrackingMock.loadState.and.resolveTo(null);

    TestBed.configureTestingModule({
      providers: [
        ExerciseStateRestoreService,
        {
          provide: AuthSessionStore,
          useValue: authSessionStoreMock,
        },
        {
          provide: BrowserService,
          useValue: browserServiceMock,
        },
        {
          provide: ExerciseProgressTrackingService,
          useValue: progressTrackingMock,
        },
      ],
    });

    service = TestBed.inject(ExerciseStateRestoreService);
  });

  it('should restore unauthenticated users from local storage without calling server', async () => {
    browserServiceMock.getItem.and.callFake((key: string) => {
      if (key === 'listen-write-state-12') {
        return 'exercise';
      }

      if (key === 'exercise-section-state-12') {
        return JSON.stringify({
          userText: 'local draft',
          autoPause: 3,
          pausedTime: 7,
          result: null,
        });
      }

      return null;
    });

    await service.restore(createRestoreRequest(12));

    expect(progressTrackingMock.loadState).not.toHaveBeenCalled();
    expect(appliedSnapshots.length).toBe(1);
    expect(appliedSnapshots[0]).toEqual(jasmine.objectContaining({
      state: 'exercise',
      userText: 'local draft',
      autoPauseSeconds: 3,
      pausedTimeSeconds: 7,
      result: null,
    }));
    expect(service.isRestoring()).toBeFalse();
  });

  it('should ignore stale server restore results from an older request', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);

    let resolveFirst!: (value: any) => void;
    let resolveSecond!: (value: any) => void;
    const firstPromise = new Promise<any>((resolve) => { resolveFirst = resolve; });
    const secondPromise = new Promise<any>((resolve) => { resolveSecond = resolve; });

    progressTrackingMock.loadState.and.callFake((exerciseId: number) => {
      if (exerciseId === 1) {
        return firstPromise;
      }

      return secondPromise;
    });

    const firstRestore = service.restore(createRestoreRequest(1));
    const secondRestore = service.restore(createRestoreRequest(2));

    resolveSecond({
      exerciseState: 'exercise',
      userText: 'newer restore',
      autoPauseSeconds: 2,
      pausedTimeSeconds: 0,
      updatedAtUtc: '2026-06-05T12:00:00.000Z',
    });
    await secondRestore;

    resolveFirst({
      exerciseState: 'intro',
      userText: 'stale restore',
      autoPauseSeconds: 5,
      pausedTimeSeconds: 0,
      updatedAtUtc: '2026-06-05T11:00:00.000Z',
    });
    await firstRestore;

    expect(appliedSnapshots.length).toBe(1);
    expect(appliedSnapshots[0]).toEqual(jasmine.objectContaining({
      state: 'exercise',
      userText: 'newer restore',
      autoPauseSeconds: 2,
    }));
  });

  it('should fall back to local state after server restore timeout', fakeAsync(() => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    progressTrackingMock.loadState.and.returnValue(new Promise(() => {}));
    browserServiceMock.getItem.and.callFake((key: string) => {
      if (key === 'listen-write-state-13') {
        return 'exercise';
      }

      if (key === 'exercise-section-state-13') {
        return JSON.stringify({
          userText: 'timeout fallback',
          autoPause: 4,
          pausedTime: 0,
          result: null,
        });
      }

      return null;
    });

    let resolved = false;
    void service.restore(createRestoreRequest(13)).then(() => {
      resolved = true;
    });

    expect(service.isRestoring()).toBeTrue();
    tick(3000);
    tick();

    expect(resolved).toBeTrue();
    expect(appliedSnapshots[0]).toEqual(jasmine.objectContaining({
      state: 'exercise',
      userText: 'timeout fallback',
      autoPauseSeconds: 4,
    }));
    expect(service.isRestoring()).toBeFalse();
  }));

  it('should sync restored completed result once after login', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    progressTrackingMock.loadState.and.resolveTo(null);
    browserServiceMock.getItem.and.callFake((key: string) => {
      if (key === 'listen-write-state-17') {
        return 'results';
      }

      if (key === 'exercise-section-state-17') {
        return JSON.stringify({
          userText: 'typed answer',
          autoPause: 2,
          pausedTime: 11,
          result: {
            accuracyPercentage: 0.6,
            userText: 'typed answer',
            originalText: 'original',
          },
          savedAtUtc: '2026-04-20T20:00:00.000Z',
        });
      }

      return null;
    });
    window.sessionStorage.setItem(
      'wf.auth.post-login-complete-sync.v1',
      JSON.stringify({ exerciseId: 17, createdAtUtc: '2026-04-20T20:01:00.000Z' }),
    );

    await service.restore(createRestoreRequest(17));
    const restored = appliedSnapshots[0];

    service.syncCompletedResultAfterRestoreIfNeeded({
      proposition: { id: 17, title: 'Exercise 17' } as any,
      exerciseState: restored.state ?? 'intro',
      result: restored.result,
    });
    service.syncCompletedResultAfterRestoreIfNeeded({
      proposition: { id: 17, title: 'Exercise 17' } as any,
      exerciseState: restored.state ?? 'intro',
      result: restored.result,
    });

    expect(progressTrackingMock.trackComplete).toHaveBeenCalledTimes(1);
    expect(window.sessionStorage.getItem('wf.auth.post-login-complete-sync.v1')).toBeNull();
  });
});

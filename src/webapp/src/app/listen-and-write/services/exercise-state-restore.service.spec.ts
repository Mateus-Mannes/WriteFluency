import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { BrowserService } from '../../core/services/browser.service';
import { GuestExerciseProgressTransferService } from '../../core/services/guest-exercise-progress-transfer.service';
import { ExerciseLocalStateStorageService } from './exercise-local-state-storage.service';
import { ExerciseProgressTrackingService } from './exercise-progress-tracking.service';
import { ExerciseStateRestoreService } from './exercise-state-restore.service';
import * as models from '../listen-and-write.models';

describe('ExerciseStateRestoreService', () => {
  let service: ExerciseStateRestoreService;
  let authSessionStoreMock: jasmine.SpyObj<AuthSessionStore>;
  let browserServiceMock: jasmine.SpyObj<BrowserService>;
  let localStateStorageMock: jasmine.SpyObj<ExerciseLocalStateStorageService>;
  let guestProgressTransferMock: jasmine.SpyObj<GuestExerciseProgressTransferService>;
  let progressTrackingMock: jasmine.SpyObj<ExerciseProgressTrackingService>;
  let appliedSnapshots: models.RestoredExerciseSnapshot[];

  const createRestoreRequest = (exerciseId: number | null) => ({
    exerciseId,
    applySnapshot: (snapshot: models.RestoredExerciseSnapshot) => {
      appliedSnapshots.push(snapshot);
    },
    resetPendingPausedTime: jasmine.createSpy('resetPendingPausedTime'),
  });

  beforeEach(() => {
    authSessionStoreMock = jasmine.createSpyObj<AuthSessionStore>(
      'AuthSessionStore',
      ['isAuthenticated', 'userId'],
    );
    browserServiceMock = jasmine.createSpyObj<BrowserService>(
      'BrowserService',
      ['isBrowserEnvironment', 'getItem', 'setItem', 'removeItem'],
    );
    localStateStorageMock = jasmine.createSpyObj<ExerciseLocalStateStorageService>(
      'ExerciseLocalStateStorageService',
      [
        'getCurrentStateKey',
        'getCurrentSnapshotKey',
        'getGuestStateKey',
        'getGuestSnapshotKey',
      ],
    );
    guestProgressTransferMock = jasmine.createSpyObj<GuestExerciseProgressTransferService>(
      'GuestExerciseProgressTransferService',
      ['consumeAuthorizedTransfer'],
    );
    progressTrackingMock = jasmine.createSpyObj<ExerciseProgressTrackingService>(
      'ExerciseProgressTrackingService',
      ['loadState', 'trackComplete'],
    );
    appliedSnapshots = [];

    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    authSessionStoreMock.userId.and.returnValue(null);
    browserServiceMock.isBrowserEnvironment.and.returnValue(true);
    browserServiceMock.getItem.and.returnValue(null);
    localStateStorageMock.getCurrentStateKey.and.callFake((id) => `current-state-${id}`);
    localStateStorageMock.getCurrentSnapshotKey.and.callFake((id) => `current-snapshot-${id}`);
    localStateStorageMock.getGuestStateKey.and.callFake((id) => `guest-state-${id}`);
    localStateStorageMock.getGuestSnapshotKey.and.callFake((id) => `guest-snapshot-${id}`);
    guestProgressTransferMock.consumeAuthorizedTransfer.and.returnValue(false);
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
          provide: ExerciseLocalStateStorageService,
          useValue: localStateStorageMock,
        },
        {
          provide: GuestExerciseProgressTransferService,
          useValue: guestProgressTransferMock,
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
      if (key === 'current-state-12') {
        return 'exercise';
      }

      if (key === 'current-snapshot-12') {
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
    authSessionStoreMock.userId.and.returnValue('user-1');

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
    authSessionStoreMock.userId.and.returnValue('user-1');
    progressTrackingMock.loadState.and.returnValue(new Promise(() => {}));
    browserServiceMock.getItem.and.callFake((key: string) => {
      if (key === 'current-state-13') {
        return 'exercise';
      }

      if (key === 'current-snapshot-13') {
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
    authSessionStoreMock.userId.and.returnValue('new-user');
    progressTrackingMock.loadState.and.resolveTo(null);
    guestProgressTransferMock.consumeAuthorizedTransfer.and.returnValue(true);
    browserServiceMock.getItem.and.callFake((key: string) => {
      if (key === 'guest-state-17') {
        return 'results';
      }

      if (key === 'guest-snapshot-17') {
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
    expect(guestProgressTransferMock.consumeAuthorizedTransfer).toHaveBeenCalledWith(17, 'new-user');
    expect(browserServiceMock.setItem).toHaveBeenCalledWith('current-state-17', 'results');
    expect(browserServiceMock.removeItem).toHaveBeenCalledWith('guest-snapshot-17');
  });

  it('should restore completed result mistake pattern metadata from server state', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    authSessionStoreMock.userId.and.returnValue('user-3');
    progressTrackingMock.loadState.and.resolveTo({
      trackingEnabled: true,
      exerciseId: 31,
      hasServerState: true,
      exerciseState: 'results',
      userText: 'typed answer',
      wordCount: 2,
      autoPauseSeconds: null,
      pausedTimeSeconds: null,
      updatedAtUtc: '2026-06-29T12:00:00.000Z',
      accuracyPercentage: 0.8,
      originalText: 'original',
      comparisons: [
        {
          sourceComparisonIndex: 0,
          originalTextRange: { initialIndex: 0, finalIndex: 7 },
          originalText: 'expected',
          userTextRange: { initialIndex: 0, finalIndex: 4 },
          userText: 'typed',
          mistakePatternTags: ['word_choice'],
          mistakePatternPhrase: 'Choose the word that preserves the intended meaning.',
        },
      ],
      correctionMode: 'normalized',
      correctionTrace: null,
    });

    await service.restore(createRestoreRequest(31));

    expect(appliedSnapshots.length).toBe(1);
    expect(appliedSnapshots[0].result?.comparisons).toEqual([
      {
        sourceComparisonIndex: 0,
        originalTextRange: { initialIndex: 0, finalIndex: 7 },
        originalText: 'expected',
        userTextRange: { initialIndex: 0, finalIndex: 4 },
        userText: 'typed',
        mistakePatternTags: ['word_choice'],
        mistakePatternPhrase: 'Choose the word that preserves the intended meaning.',
      },
    ]);
  });

  it('should not restore guest state for an authenticated user without an authorized transfer', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    authSessionStoreMock.userId.and.returnValue('user-2');
    progressTrackingMock.loadState.and.resolveTo(null);
    browserServiceMock.getItem.and.callFake((key: string) => {
      if (key === 'guest-state-22') {
        return 'exercise';
      }
      if (key === 'guest-snapshot-22') {
        return JSON.stringify({ userText: 'guest draft' });
      }
      return null;
    });

    await service.restore(createRestoreRequest(22));

    expect(appliedSnapshots).toEqual([]);
    expect(guestProgressTransferMock.consumeAuthorizedTransfer).toHaveBeenCalledWith(22, 'user-2');
    expect(browserServiceMock.getItem).not.toHaveBeenCalledWith('guest-state-22');
  });
});

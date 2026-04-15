import { signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';

import { ListenAndWriteComponent, LISTEN_WRITE_FIRST_TIME_KEY } from './listen-and-write.component';
import { ExerciseProgressTrackingService } from './services/exercise-progress-tracking.service';
import { AuthSessionStore } from '../auth/services/auth-session.store';

const guestBeginAttemptCountStorageKey = 'wf.guest.begin.exercise.attempt.v1';
const guestBeginLoginModalLastShownStorageKey = 'wf.guest.login.modal.last-shown-utc.v1';

describe('ListenAndWriteComponent', () => {
  let component: ListenAndWriteComponent;
  let fixture: ComponentFixture<ListenAndWriteComponent>;
  let routeParams$: Subject<Record<string, unknown>>;
  let progressSyncNotificationSignal: ReturnType<typeof signal>;
  let authSessionStoreMock: jasmine.SpyObj<AuthSessionStore>;
  let exerciseProgressTrackingMock: {
    trackStart: jasmine.Spy;
    trackComplete: jasmine.Spy;
    saveState: jasmine.Spy;
    loadState: jasmine.Spy;
    dismissSyncNotification: jasmine.Spy;
    syncNotification: () => any;
  };

  beforeEach(async () => {
    window.localStorage.removeItem(LISTEN_WRITE_FIRST_TIME_KEY);
    window.localStorage.removeItem(guestBeginAttemptCountStorageKey);
    window.localStorage.removeItem(guestBeginLoginModalLastShownStorageKey);

    routeParams$ = new Subject<Record<string, unknown>>();
    progressSyncNotificationSignal = signal<any>(null);
    authSessionStoreMock = jasmine.createSpyObj<AuthSessionStore>(
      'AuthSessionStore',
      ['isAuthenticated'],
    );
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    exerciseProgressTrackingMock = {
      trackStart: jasmine.createSpy('trackStart'),
      trackComplete: jasmine.createSpy('trackComplete'),
      saveState: jasmine.createSpy('saveState'),
      loadState: jasmine.createSpy('loadState').and.resolveTo(null),
      dismissSyncNotification: jasmine.createSpy('dismissSyncNotification'),
      syncNotification: progressSyncNotificationSignal.asReadonly(),
    };

    await TestBed.configureTestingModule({
      imports: [ListenAndWriteComponent],
      providers: [
        {
          provide: ExerciseProgressTrackingService,
          useValue: exerciseProgressTrackingMock,
        },
        {
          provide: ActivatedRoute,
          useValue: {
            params: routeParams$.asObservable(),
            queryParams: routeParams$.asObservable()
          }
        },
        {
          provide: AuthSessionStore,
          useValue: authSessionStoreMock,
        }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ListenAndWriteComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should use auto-pause value for rewind shortcut when enabled', () => {
    const rewindAudio = jasmine.createSpy('rewindAudio');

    component.exerciseState.set('exercise');
    component.exerciseSectionComponent = {
      selectedAutoPause: () => 5
    } as any;
    component.newsAudioComponent = {
      rewindAudio,
      forwardAudio: jasmine.createSpy('forwardAudio'),
      isAudioPlaying: () => false
    } as any;

    const event = {
      ctrlKey: true,
      metaKey: false,
      key: 'ArrowLeft',
      code: 'ArrowLeft',
      preventDefault: jasmine.createSpy('preventDefault')
    } as unknown as KeyboardEvent;

    component.handleKeyboardEvent(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(rewindAudio).toHaveBeenCalledWith(5);
  });

  it('should use 3 seconds for forward shortcut when auto-pause is off', () => {
    const forwardAudio = jasmine.createSpy('forwardAudio');

    component.exerciseState.set('exercise');
    component.exerciseSectionComponent = {
      selectedAutoPause: () => 0
    } as any;
    component.newsAudioComponent = {
      rewindAudio: jasmine.createSpy('rewindAudio'),
      forwardAudio,
      isAudioPlaying: () => false
    } as any;

    const event = {
      ctrlKey: true,
      metaKey: false,
      key: 'ArrowRight',
      code: 'ArrowRight',
      preventDefault: jasmine.createSpy('preventDefault')
    } as unknown as KeyboardEvent;

    component.handleKeyboardEvent(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(forwardAudio).toHaveBeenCalledWith(3);
  });

  it('should save progress state when audio starts during exercise', () => {
    component.exerciseId = 55;
    component.exerciseState.set('exercise');
    component.result.set(null);
    component.proposition.set({ id: 55, title: 'Exercise 55' } as any);
    component.exerciseSectionComponent = {
      text: () => 'one two',
      selectedAutoPause: () => 0,
      blurTextArea: jasmine.createSpy('blurTextArea'),
    } as any;
    component.newsAudioComponent = {
      audioRef: { nativeElement: { currentTime: 8 } },
      isAudioPlaying: () => false,
    } as any;

    component.onAudioPlayClicked();

    expect(exerciseProgressTrackingMock.saveState).toHaveBeenCalled();
  });

  it('should save progress state when audio seek happens during exercise', () => {
    component.exerciseId = 56;
    component.exerciseState.set('exercise');
    component.result.set(null);
    component.proposition.set({ id: 56, title: 'Exercise 56' } as any);
    component.exerciseSectionComponent = {
      text: () => 'one two three',
      selectedAutoPause: () => 2,
    } as any;
    component.newsAudioComponent = {
      audioRef: { nativeElement: { currentTime: 14 } },
      isAudioPlaying: () => false,
    } as any;

    component.onAudioSeeked();

    expect(exerciseProgressTrackingMock.saveState).toHaveBeenCalled();
  });

  it('should render inline progress sync toast when tracking service exposes one', () => {
    progressSyncNotificationSignal.set({
      id: 1,
      kind: 'warning',
      message: 'We had a problem saving your progress.',
    });
    fixture.detectChanges();

    const toast = fixture.nativeElement.querySelector('.progress-sync-toast');
    expect(toast).toBeTruthy();
    expect(toast.textContent).toContain('problem saving your progress');
  });

  it('should dismiss progress sync toast when dismiss is clicked', () => {
    progressSyncNotificationSignal.set({
      id: 2,
      kind: 'session_expired',
      message: 'Please log in again.',
    });
    fixture.detectChanges();

    const dismissButton = fixture.nativeElement.querySelector('.progress-sync-toast button');
    expect(dismissButton).toBeTruthy();

    dismissButton.click();
    expect(exerciseProgressTrackingMock.dismissSyncNotification).toHaveBeenCalled();
  });

  it('should restore from server first when authenticated and skip local fallback', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    component.exerciseId = 42;
    const browserService = (component as any).browserService;
    const getItemSpy = spyOn(browserService, 'getItem').and.returnValue(null);
    exerciseProgressTrackingMock.loadState.and.resolveTo({
      trackingEnabled: true,
      exerciseId: 42,
      hasServerState: true,
      exerciseState: 'exercise',
      userText: 'server text',
      wordCount: 2,
      autoPauseSeconds: 4,
      pausedTimeSeconds: 8,
      updatedAtUtc: new Date().toISOString(),
    });

    await component.restoreExerciseState();

    expect(exerciseProgressTrackingMock.loadState).toHaveBeenCalledWith(42);
    expect(getItemSpy).not.toHaveBeenCalled();
    expect(component.exerciseState()).toBe('exercise');
    expect(component.initialText()).toBe('server text');
    expect(component.initialAutoPause()).toBe(4);
    expect(component.isRestoringExercise()).toBeFalse();
  });

  it('should fallback to local storage when authenticated server restore has no state', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    component.exerciseId = 10;
    exerciseProgressTrackingMock.loadState.and.resolveTo(null);
    const browserService = (component as any).browserService;
    spyOn(browserService, 'getItem').and.callFake((key: string) => {
      if (key === 'listen-write-state-10') {
        return 'exercise';
      }

      if (key === 'exercise-section-state-10') {
        return JSON.stringify({
          userText: 'local draft',
          autoPause: 3,
          pausedTime: 5,
          result: null,
        });
      }

      return null;
    });

    await component.restoreExerciseState();

    expect(exerciseProgressTrackingMock.loadState).toHaveBeenCalledWith(10);
    expect(component.exerciseState()).toBe('exercise');
    expect(component.initialText()).toBe('local draft');
    expect(component.initialAutoPause()).toBe(3);
    expect(component.isRestoringExercise()).toBeFalse();
  });

  it('should fallback to local storage after server restore timeout', fakeAsync(() => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    component.exerciseId = 12;
    exerciseProgressTrackingMock.loadState.and.returnValue(new Promise(() => {}));
    const browserService = (component as any).browserService;
    spyOn(browserService, 'getItem').and.callFake((key: string) => {
      if (key === 'listen-write-state-12') {
        return 'exercise';
      }

      if (key === 'exercise-section-state-12') {
        return JSON.stringify({
          userText: 'timeout fallback',
          autoPause: 2,
          pausedTime: 0,
          result: null,
        });
      }

      return null;
    });

    let resolved = false;
    void component.restoreExerciseState().then(() => {
      resolved = true;
    });

    expect(component.isRestoringExercise()).toBeTrue();
    tick(3000);
    tick();

    expect(resolved).toBeTrue();
    expect(component.exerciseState()).toBe('exercise');
    expect(component.initialText()).toBe('timeout fallback');
    expect(component.isRestoringExercise()).toBeFalse();
  }));

  it('should restore from local storage without calling server when unauthenticated', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    component.exerciseId = 15;
    const browserService = (component as any).browserService;
    spyOn(browserService, 'getItem').and.callFake((key: string) => {
      if (key === 'listen-write-state-15') {
        return 'exercise';
      }

      if (key === 'exercise-section-state-15') {
        return JSON.stringify({
          userText: 'unauthenticated local',
          autoPause: 2,
          pausedTime: 0,
          result: null,
        });
      }

      return null;
    });

    await component.restoreExerciseState();

    expect(exerciseProgressTrackingMock.loadState).not.toHaveBeenCalled();
    expect(component.exerciseState()).toBe('exercise');
    expect(component.initialText()).toBe('unauthenticated local');
    expect(component.isRestoringExercise()).toBeFalse();
  });

  it('should ignore stale restore results from older requests', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);

    let resolveFirst!: (value: any) => void;
    let resolveSecond!: (value: any) => void;
    const firstPromise = new Promise<any>((resolve) => { resolveFirst = resolve; });
    const secondPromise = new Promise<any>((resolve) => { resolveSecond = resolve; });

    exerciseProgressTrackingMock.loadState.and.callFake((exerciseId: number) => {
      if (exerciseId === 1) {
        return firstPromise;
      }

      return secondPromise;
    });

    component.exerciseId = 1;
    const firstRestore = component.restoreExerciseState();
    component.exerciseId = 2;
    const secondRestore = component.restoreExerciseState();

    resolveSecond({
      trackingEnabled: true,
      exerciseId: 2,
      hasServerState: true,
      exerciseState: 'exercise',
      userText: 'newer restore',
      wordCount: 2,
      autoPauseSeconds: 2,
      pausedTimeSeconds: 0,
      updatedAtUtc: new Date().toISOString(),
    });
    await secondRestore;

    resolveFirst({
      trackingEnabled: true,
      exerciseId: 1,
      hasServerState: true,
      exerciseState: 'intro',
      userText: 'stale restore',
      wordCount: 2,
      autoPauseSeconds: 5,
      pausedTimeSeconds: 0,
      updatedAtUtc: new Date().toISOString(),
    });
    await firstRestore;

    expect(component.exerciseState()).toBe('exercise');
    expect(component.initialText()).toBe('newer restore');
    expect(component.initialAutoPause()).toBe(2);
  });

  it('should restore only once on route load', async () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    exerciseProgressTrackingMock.loadState.and.resolveTo(null);
    const browserService = (component as any).browserService;
    spyOn(browserService, 'getItem').and.returnValue(null);
    spyOn(component, 'loadProposition').and.stub();

    routeParams$.next({ id: '77' });
    await fixture.whenStable();

    expect(exerciseProgressTrackingMock.loadState).toHaveBeenCalledTimes(1);
    expect(exerciseProgressTrackingMock.loadState).toHaveBeenCalledWith(77);
  });

  it('should render login CTA on results when user is not authenticated', () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    component.exerciseState.set('results');

    fixture.detectChanges();

    const cta = fixture.nativeElement.querySelector('.exercise-login-cta');
    expect(cta).toBeTruthy();
    expect(cta.textContent).toContain('Sign in to save');
  });

  it('should hide login CTA on results when user is authenticated', () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(true);
    component.exerciseState.set('results');

    fixture.detectChanges();

    const cta = fixture.nativeElement.querySelector('.exercise-login-cta');
    expect(cta).toBeNull();
  });

  it('should navigate to login with returnUrl when CTA is clicked', () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    component.exerciseId = 88;
    component.exerciseState.set('results');

    const router = (component as any).router;
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture.detectChanges();
    const ctaButton = fixture.nativeElement.querySelector('.exercise-login-cta-primary');
    expect(ctaButton).toBeTruthy();

    ctaButton.click();

    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: {
        returnUrl: '/english-writing-exercise/88',
        source: 'results_save_cta',
      }
    });
  });

  it('should show guest login modal on second begin attempt when user is not authenticated', () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    window.localStorage.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');

    component.exerciseState.set('intro');
    component.newsAudioComponent = {
      audioEnded: false,
      pauseAudio: jasmine.createSpy('pauseAudio'),
      playAudio: jasmine.createSpy('playAudio'),
      audioRef: { nativeElement: { currentTime: 12 } },
    } as any;

    component.beginExercise();
    expect(component.isGuestLoginModalOpen()).toBeFalse();
    expect(component.exerciseState()).toBe('exercise');

    component.setNewState('intro');
    component.beginExercise();

    expect(component.isGuestLoginModalOpen()).toBeTrue();
    expect(component.exerciseState()).toBe('intro');
  });

  it('should continue exercise start when guest chooses continue as guest in begin modal', () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    window.localStorage.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
    window.localStorage.setItem(guestBeginAttemptCountStorageKey, '1');

    component.exerciseState.set('intro');
    component.newsAudioComponent = {
      audioEnded: false,
      pauseAudio: jasmine.createSpy('pauseAudio'),
      playAudio: jasmine.createSpy('playAudio'),
      audioRef: { nativeElement: { currentTime: 0 } },
    } as any;

    component.beginExercise();
    expect(component.isGuestLoginModalOpen()).toBeTrue();

    component.onGuestLoginModalContinueAsGuest();

    expect(component.isGuestLoginModalOpen()).toBeFalse();
    expect(component.exerciseState()).toBe('exercise');
  });

  it('should navigate to login with returnUrl when guest begin modal sign-in CTA is clicked', () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    window.localStorage.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
    window.localStorage.setItem(guestBeginAttemptCountStorageKey, '1');

    component.exerciseId = 99;
    component.exerciseState.set('intro');
    component.newsAudioComponent = {
      audioEnded: false,
      pauseAudio: jasmine.createSpy('pauseAudio'),
      playAudio: jasmine.createSpy('playAudio'),
      audioRef: { nativeElement: { currentTime: 0 } },
    } as any;

    const router = (component as any).router;
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    component.beginExercise();
    expect(component.isGuestLoginModalOpen()).toBeTrue();

    component.onGuestLoginModalSignIn();

    expect(component.isGuestLoginModalOpen()).toBeFalse();
    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: {
        returnUrl: '/english-writing-exercise/99',
        source: 'begin_exercise_modal',
      }
    });
  });

  it('should respect cooldown and skip begin modal when it was recently shown', () => {
    authSessionStoreMock.isAuthenticated.and.returnValue(false);
    window.localStorage.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
    window.localStorage.setItem(guestBeginAttemptCountStorageKey, '4');
    window.localStorage.setItem(guestBeginLoginModalLastShownStorageKey, new Date().toISOString());

    component.exerciseState.set('intro');
    component.newsAudioComponent = {
      audioEnded: false,
      pauseAudio: jasmine.createSpy('pauseAudio'),
      playAudio: jasmine.createSpy('playAudio'),
      audioRef: { nativeElement: { currentTime: 0 } },
    } as any;

    component.beginExercise();

    expect(component.isGuestLoginModalOpen()).toBeFalse();
    expect(component.exerciseState()).toBe('exercise');
  });
});

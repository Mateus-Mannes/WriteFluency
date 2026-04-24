import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { ShepherdService } from 'angular-shepherd';
import { BrowserService } from '../../core/services/browser.service';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { LISTEN_WRITE_FIRST_TIME_KEY } from '../listen-and-write.component';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { ExerciseTourService } from './exercise-tour.service';

describe('ExerciseTourService', () => {
  let service: ExerciseTourService;
  let shepherdMock: any;
  let browserServiceMock: jasmine.SpyObj<BrowserService>;
  let exerciseSessionTrackingMock: jasmine.SpyObj<ExerciseSessionTrackingService>;
  let authSessionStoreMock: jasmine.SpyObj<AuthSessionStore>;
  let tourCallbacks: Record<string, () => void>;

  beforeEach(() => {
    tourCallbacks = {};
    shepherdMock = {
      isActive: false,
      defaultStepOptions: undefined,
      modal: false,
      keyboardNavigation: true,
      complete: jasmine.createSpy('complete'),
      cancel: jasmine.createSpy('cancel'),
      next: jasmine.createSpy('next'),
      back: jasmine.createSpy('back'),
      addSteps: jasmine.createSpy('addSteps'),
      start: jasmine.createSpy('start'),
      tourObject: {
        on: jasmine.createSpy('on').and.callFake((eventName: string, callback: () => void) => {
          tourCallbacks[eventName] = callback;
        }),
      },
    };

    browserServiceMock = jasmine.createSpyObj<BrowserService>('BrowserService', ['setItem']);
    exerciseSessionTrackingMock = jasmine.createSpyObj<ExerciseSessionTrackingService>('ExerciseSessionTrackingService', ['trackEvent']);
    authSessionStoreMock = jasmine.createSpyObj<AuthSessionStore>('AuthSessionStore', [
      'markListenWriteTutorialCompletedInBackground',
    ]);

    TestBed.configureTestingModule({
      providers: [
        ExerciseTourService,
        { provide: ShepherdService, useValue: shepherdMock },
        { provide: BrowserService, useValue: browserServiceMock },
        { provide: ExerciseSessionTrackingService, useValue: exerciseSessionTrackingMock },
        { provide: AuthSessionStore, useValue: authSessionStoreMock },
      ],
    });

    service = TestBed.inject(ExerciseTourService);
  });

  it('should mark tutorial done locally and sync in background when desktop tour completes', fakeAsync(() => {
    service.startTour();
    tick(51);

    expect(shepherdMock.start).toHaveBeenCalled();
    tourCallbacks['complete']?.();

    expect(browserServiceMock.setItem).toHaveBeenCalledWith(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
    expect(authSessionStoreMock.markListenWriteTutorialCompletedInBackground).toHaveBeenCalled();
  }));

  it('should mark tutorial done locally and sync in background when desktop tour is cancelled', fakeAsync(() => {
    service.startTour();
    tick(51);

    tourCallbacks['cancel']?.();

    expect(browserServiceMock.setItem).toHaveBeenCalledWith(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
    expect(authSessionStoreMock.markListenWriteTutorialCompletedInBackground).toHaveBeenCalled();
  }));

  it('should mark tutorial done locally and sync in background when mobile tour is cancelled', fakeAsync(() => {
    service.startMobileTour();
    tick(51);

    tourCallbacks['cancel']?.();

    expect(browserServiceMock.setItem).toHaveBeenCalledWith(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
    expect(authSessionStoreMock.markListenWriteTutorialCompletedInBackground).toHaveBeenCalled();
  }));
});

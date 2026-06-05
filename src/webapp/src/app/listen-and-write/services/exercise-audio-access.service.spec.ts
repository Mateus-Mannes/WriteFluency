import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { PropositionsService } from '../../../api/listen-and-write/api/propositions.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { ExerciseAudioAccessService, type AudioAccessCallbacks } from './exercise-audio-access.service';
import * as models from '../listen-and-write.models';

describe('ExerciseAudioAccessService', () => {
  let service: ExerciseAudioAccessService;
  let propositionsServiceMock: jasmine.SpyObj<PropositionsService>;
  let trackingMock: jasmine.SpyObj<ExerciseSessionTrackingService>;
  let callbacks: jasmine.SpyObj<AudioAccessCallbacks>;

  const beginContext: models.BeginExerciseContext = {
    isFirstTimeUser: false,
    audioEndedBeforeBegin: false,
    guestBeginAttemptCount: null,
    guestLoginModalShownBeforeStart: false,
  };

  beforeEach(() => {
    propositionsServiceMock = jasmine.createSpyObj<PropositionsService>(
      'PropositionsService',
      ['apiPropositionIdBeginPost'],
    );
    trackingMock = jasmine.createSpyObj<ExerciseSessionTrackingService>(
      'ExerciseSessionTrackingService',
      ['trackEvent'],
    );
    callbacks = jasmine.createSpyObj<AudioAccessCallbacks>(
      'AudioAccessCallbacks',
      ['onMetadata', 'onProRequired', 'onGranted', 'onMissingAudio', 'onError'],
    );

    TestBed.configureTestingModule({
      providers: [
        ExerciseAudioAccessService,
        {
          provide: PropositionsService,
          useValue: propositionsServiceMock,
        },
        {
          provide: ExerciseSessionTrackingService,
          useValue: trackingMock,
        },
      ],
    });

    service = TestBed.inject(ExerciseAudioAccessService);
  });

  it('should store granted audio access and continue the pending begin context', () => {
    const metadata = { id: 42, title: 'Exercise 42' } as any;
    propositionsServiceMock.apiPropositionIdBeginPost.and.returnValue(of({
      access: 'granted',
      audioUrl: 'https://audio.test/exercise.mp3',
      audioExpiresAtUtc: '2026-06-05T12:00:00.000Z',
      metadata,
    } as any) as any);

    service.resolve({
      exerciseId: 42,
      startWritingWhenGranted: true,
      context: beginContext,
      callbacks,
    });

    expect(service.audioUrl()).toBe('https://audio.test/exercise.mp3');
    expect(service.getAudioExpiresAtUtc()).toBe('2026-06-05T12:00:00.000Z');
    expect(service.isResolving()).toBeFalse();
    expect(service.isBeginningExercise()).toBeFalse();
    expect(callbacks.onMetadata).toHaveBeenCalledWith(metadata);
    expect(callbacks.onGranted).toHaveBeenCalledWith(beginContext);
  });

  it('should clear audio and call Pro denial handling when access requires Pro', () => {
    service.audioUrl.set('https://audio.test/old.mp3');
    propositionsServiceMock.apiPropositionIdBeginPost.and.returnValue(of({
      access: 'pro_required',
      audioUrl: null,
      audioExpiresAtUtc: null,
      metadata: { id: 43, title: 'Exercise 43' },
    } as any) as any);

    service.resolve({
      exerciseId: 43,
      startWritingWhenGranted: false,
      callbacks,
    });

    expect(service.audioUrl()).toBeNull();
    expect(service.getAudioExpiresAtUtc()).toBeNull();
    expect(callbacks.onProRequired).toHaveBeenCalled();
    expect(callbacks.onGranted).not.toHaveBeenCalled();
  });

  it('should ignore stale responses after a newer request starts', () => {
    const staleResponse$ = new Subject<any>();
    propositionsServiceMock.apiPropositionIdBeginPost.and.returnValues(
      staleResponse$.asObservable() as any,
      of({
        access: 'granted',
        audioUrl: 'https://audio.test/current.mp3',
        audioExpiresAtUtc: '2026-06-05T12:00:00.000Z',
      } as any) as any,
    );

    service.resolve({
      exerciseId: 44,
      startWritingWhenGranted: false,
      callbacks,
    });
    service.resolve({
      exerciseId: 44,
      startWritingWhenGranted: false,
      callbacks,
    });

    staleResponse$.next({
      access: 'granted',
      audioUrl: 'https://audio.test/stale.mp3',
      audioExpiresAtUtc: '2026-06-05T11:00:00.000Z',
    });

    expect(service.audioUrl()).toBe('https://audio.test/current.mp3');
  });

  it('should only resolve hydrated intro exercises once', () => {
    propositionsServiceMock.apiPropositionIdBeginPost.and.returnValue(of({
      access: 'granted',
      audioUrl: 'https://audio.test/hydrated.mp3',
      audioExpiresAtUtc: null,
    } as any) as any);

    service.tryResolveAfterHydration({
      exerciseId: 45,
      hasHydrated: false,
      isBrowserEnvironment: true,
      hasProposition: true,
      exerciseState: 'intro',
      callbacks,
    });

    expect(propositionsServiceMock.apiPropositionIdBeginPost).not.toHaveBeenCalled();

    service.tryResolveAfterHydration({
      exerciseId: 45,
      hasHydrated: true,
      isBrowserEnvironment: true,
      hasProposition: true,
      exerciseState: 'intro',
      callbacks,
    });
    service.tryResolveAfterHydration({
      exerciseId: 45,
      hasHydrated: true,
      isBrowserEnvironment: true,
      hasProposition: true,
      exerciseState: 'intro',
      callbacks,
    });

    expect(propositionsServiceMock.apiPropositionIdBeginPost).toHaveBeenCalledTimes(1);
  });
});

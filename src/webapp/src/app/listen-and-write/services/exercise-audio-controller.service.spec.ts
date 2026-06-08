import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { BrowserService } from '../../core/services/browser.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import {
  ExerciseAudioControl,
  ExerciseAudioControlContext,
  ExerciseAudioControllerService,
  ExerciseAudioSectionControl,
} from './exercise-audio-controller.service';

describe('ExerciseAudioControllerService', () => {
  let service: ExerciseAudioControllerService;
  let browserServiceMock: jasmine.SpyObj<BrowserService>;
  let trackingMock: jasmine.SpyObj<ExerciseSessionTrackingService>;
  let audio: jasmine.SpyObj<ExerciseAudioControl>;
  let section: jasmine.SpyObj<ExerciseAudioSectionControl>;

  function createContext(overrides: Partial<ExerciseAudioControlContext> = {}): ExerciseAudioControlContext {
    return {
      exerciseState: 'exercise',
      audio,
      section,
      isMobileLayout: false,
      onSaveExerciseState: jasmine.createSpy('onSaveExerciseState'),
      onCancelListenFirstTour: jasmine.createSpy('onCancelListenFirstTour'),
      onFinishExerciseTour: jasmine.createSpy('onFinishExerciseTour'),
      ...overrides,
    };
  }

  beforeEach(() => {
    browserServiceMock = jasmine.createSpyObj<BrowserService>(
      'BrowserService',
      ['blurActiveElement'],
    );
    trackingMock = jasmine.createSpyObj<ExerciseSessionTrackingService>(
      'ExerciseSessionTrackingService',
      ['trackEvent'],
    );
    audio = jasmine.createSpyObj<ExerciseAudioControl>(
      'ExerciseAudioControl',
      ['isAudioPlaying', 'playAudio', 'pauseAudio', 'resetAudioToStart', 'rewindAudio', 'forwardAudio'],
    );
    section = jasmine.createSpyObj<ExerciseAudioSectionControl>(
      'ExerciseAudioSectionControl',
      ['selectedAutoPause', 'blurTextArea', 'focusTextArea'],
    );

    audio.isAudioPlaying.and.returnValue(false);
    section.selectedAutoPause.and.returnValue(0);

    TestBed.configureTestingModule({
      providers: [
        ExerciseAudioControllerService,
        {
          provide: BrowserService,
          useValue: browserServiceMock,
        },
        {
          provide: ExerciseSessionTrackingService,
          useValue: trackingMock,
        },
      ],
    });

    service = TestBed.inject(ExerciseAudioControllerService);
  });

  it('should play audio, finish the tour, and mark keyboard shortcut as the next play source', () => {
    const context = createContext();
    const event = {
      ctrlKey: true,
      metaKey: false,
      key: 'Enter',
      code: 'Enter',
      preventDefault: jasmine.createSpy('preventDefault'),
    } as unknown as KeyboardEvent;

    service.handleKeyboardEvent(event, context);
    service.onAudioPlayClicked(context);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(audio.playAudio).toHaveBeenCalled();
    expect(context.onFinishExerciseTour).toHaveBeenCalled();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'audio_shortcut_used',
      jasmine.objectContaining({
        shortcut: 'ctrl_or_cmd_enter',
        action: 'play',
      }),
    );
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'audio_play_clicked',
      jasmine.objectContaining({
        exercise_state: 'exercise',
        play_source: 'keyboard_shortcut',
      }),
    );
  });

  it('should pause audio and clear the timer from the play/pause shortcut when already playing', fakeAsync(() => {
    audio.isAudioPlaying.and.returnValue(true);
    section.selectedAutoPause.and.returnValue(1);
    const context = createContext();

    service.applyAutoPause(context);
    service.handleKeyboardEvent({
      ctrlKey: true,
      metaKey: false,
      key: 'Enter',
      code: 'Enter',
      preventDefault: jasmine.createSpy('preventDefault'),
    } as unknown as KeyboardEvent, context);

    tick(1000);

    expect(audio.pauseAudio).toHaveBeenCalledTimes(1);
  }));

  it('should use the selected auto-pause seconds for rewind shortcuts', () => {
    section.selectedAutoPause.and.returnValue(5);
    const event = {
      ctrlKey: true,
      metaKey: false,
      key: 'ArrowLeft',
      code: 'ArrowLeft',
      preventDefault: jasmine.createSpy('preventDefault'),
    } as unknown as KeyboardEvent;

    service.handleKeyboardEvent(event, createContext());

    expect(event.preventDefault).toHaveBeenCalled();
    expect(audio.rewindAudio).toHaveBeenCalledWith(5);
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'audio_shortcut_used',
      jasmine.objectContaining({
        shortcut: 'ctrl_or_cmd_arrow_left',
        action: 'rewind',
      }),
      jasmine.objectContaining({
        seek_seconds: 5,
      }),
    );
  });

  it('should use three seconds for forward shortcuts when auto-pause is off', () => {
    section.selectedAutoPause.and.returnValue(0);
    const event = {
      ctrlKey: true,
      metaKey: false,
      key: 'ArrowRight',
      code: 'ArrowRight',
      preventDefault: jasmine.createSpy('preventDefault'),
    } as unknown as KeyboardEvent;

    service.handleKeyboardEvent(event, createContext());

    expect(event.preventDefault).toHaveBeenCalled();
    expect(audio.forwardAudio).toHaveBeenCalledWith(3);
  });

  it('should pause audio after the selected auto-pause duration', fakeAsync(() => {
    section.selectedAutoPause.and.returnValue(2);
    audio.isAudioPlaying.and.returnValue(true);

    service.applyAutoPause(createContext());

    expect(section.blurTextArea).toHaveBeenCalled();
    expect(browserServiceMock.blurActiveElement).toHaveBeenCalled();

    tick(1999);
    expect(audio.pauseAudio).not.toHaveBeenCalled();

    tick(1);
    expect(audio.pauseAudio).toHaveBeenCalled();
  }));

  it('should save state and restore text focus after an exercise audio pause', () => {
    const context = createContext();

    service.onAudioPaused(context);

    expect(context.onSaveExerciseState).toHaveBeenCalled();
    expect(section.focusTextArea).toHaveBeenCalled();
  });

  it('should apply pending paused time when the audio control becomes available', () => {
    service.setPendingPausedTime(12, null);
    expect(audio.forwardAudio).not.toHaveBeenCalled();

    service.applyPendingPausedTimeIfNeeded(audio);

    expect(audio.forwardAudio).toHaveBeenCalledWith(12);
  });

  it('should pause, reset to the beginning, and clear the auto-pause timer', fakeAsync(() => {
    section.selectedAutoPause.and.returnValue(1);
    audio.isAudioPlaying.and.returnValue(true);

    service.applyAutoPause(createContext());
    service.pauseAndResetAudio(audio);
    tick(1000);

    expect(audio.pauseAudio).toHaveBeenCalledTimes(1);
    expect(audio.resetAudioToStart).toHaveBeenCalled();
  }));

  it('should mark listen-first prompt as the next play source', () => {
    const context = createContext({
      exerciseState: 'intro',
    });

    service.playAudioFromListenFirstPrompt(audio);
    service.onAudioPlayClicked(context);

    expect(audio.playAudio).toHaveBeenCalled();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'audio_play_clicked',
      jasmine.objectContaining({
        exercise_state: 'intro',
        play_source: 'listen_first_prompt',
      }),
    );
  });
});

import { Injectable } from '@angular/core';
import { BrowserService } from '../../core/services/browser.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import * as models from '../listen-and-write.models';

export interface ExerciseAudioControl {
  isAudioPlaying(): boolean;
  playAudio(): void;
  pauseAudio(): void;
  resetAudioToStart?(): void;
  rewindAudio(seconds: number): void;
  forwardAudio(seconds: number): void;
}

export interface ExerciseAudioSectionControl {
  selectedAutoPause(): number;
  blurTextArea(): void;
  focusTextArea(): void;
}

export interface ExerciseAudioControlContext {
  exerciseState: models.ExerciseState;
  audio: ExerciseAudioControl | null;
  section: ExerciseAudioSectionControl | null;
  isMobileLayout: boolean;
  onSaveExerciseState: () => void;
  onCancelListenFirstTour: () => void;
  onFinishExerciseTour: () => void;
}

@Injectable()
export class ExerciseAudioControllerService {
  private autoPauseTimer: ReturnType<typeof setTimeout> | null = null;
  private pendingAudioPlaySource: models.AudioPlaySource | null = null;
  private pendingAudioPlaySourceTimer: ReturnType<typeof setTimeout> | null = null;
  private pendingPausedTimeSeconds: number | null = null;

  constructor(
    private readonly browserService: BrowserService,
    private readonly exerciseSessionTracking: ExerciseSessionTrackingService,
  ) {}

  reset(): void {
    this.resetPendingPausedTime();
    this.clearAutoPauseTimer();
    this.clearPendingAudioPlaySource();
  }

  resetPendingPausedTime(): void {
    this.pendingPausedTimeSeconds = null;
  }

  setPendingPausedTime(pausedTimeSeconds: number | null, audio: ExerciseAudioControl | null): void {
    if (!pausedTimeSeconds || pausedTimeSeconds <= 0) {
      this.pendingPausedTimeSeconds = null;
      return;
    }

    this.pendingPausedTimeSeconds = pausedTimeSeconds;
    this.applyPendingPausedTimeIfNeeded(audio);
  }

  applyPendingPausedTimeIfNeeded(audio: ExerciseAudioControl | null): void {
    if (!this.pendingPausedTimeSeconds || !audio) {
      return;
    }

    audio.forwardAudio(this.pendingPausedTimeSeconds);
    this.pendingPausedTimeSeconds = null;
  }

  handleKeyboardEvent(event: KeyboardEvent, context: ExerciseAudioControlContext): void {
    if (context.exerciseState !== 'exercise' || !context.audio) {
      return;
    }

    const modifierKey = event.ctrlKey || event.metaKey;
    if (!modifierKey) {
      return;
    }

    if (event.key === 'Enter') {
      this.handlePlayPauseShortcut(event, context);
      return;
    }

    if (event.code === 'ArrowLeft') {
      this.handleSeekShortcut(event, context, 'rewind');
      return;
    }

    if (event.code === 'ArrowRight') {
      this.handleSeekShortcut(event, context, 'forward');
    }
  }

  playAudioWithAutoPause(context: ExerciseAudioControlContext): void {
    context.audio?.playAudio();
    this.applyAutoPause(context);
  }

  applyAutoPause(context: ExerciseAudioControlContext): void {
    this.clearAutoPauseTimer();
    if (context.isMobileLayout) {
      return;
    }

    context.section?.blurTextArea();
    this.browserService.blurActiveElement();

    const duration = context.section?.selectedAutoPause?.() ?? 0;
    if (duration <= 0) {
      return;
    }

    this.autoPauseTimer = setTimeout(() => {
      if (context.audio?.isAudioPlaying()) {
        context.audio.pauseAudio();
      }

      this.clearAutoPauseTimer();
    }, duration * 1000);
  }

  pauseAudioWithTimerClear(audio: ExerciseAudioControl | null): void {
    audio?.pauseAudio();
    this.clearAutoPauseTimer();
  }

  pauseAudio(audio: ExerciseAudioControl | null): void {
    audio?.pauseAudio();
  }

  pauseAndResetAudio(audio: ExerciseAudioControl | null): void {
    audio?.pauseAudio();
    audio?.resetAudioToStart?.();
    this.clearAutoPauseTimer();
  }

  playAudioFromListenFirstPrompt(audio: ExerciseAudioControl | null): void {
    this.markNextAudioPlaySource('listen_first_prompt');
    audio?.playAudio();
  }

  clearAutoPauseTimer(): void {
    if (!this.autoPauseTimer) {
      return;
    }

    clearTimeout(this.autoPauseTimer);
    this.autoPauseTimer = null;
  }

  onAudioPlayClicked(context: ExerciseAudioControlContext): void {
    const playSource = this.consumePendingAudioPlaySource();
    this.exerciseSessionTracking.trackEvent('audio_play_clicked', {
      exercise_state: context.exerciseState,
      play_source: playSource,
    });

    context.onCancelListenFirstTour();

    if (context.exerciseState !== 'exercise') {
      return;
    }

    this.applyAutoPause(context);
    context.onSaveExerciseState();
  }

  onAudioPaused(context: ExerciseAudioControlContext): void {
    if (context.exerciseState !== 'exercise') {
      return;
    }

    context.onSaveExerciseState();
    context.section?.focusTextArea();
  }

  onAudioSeeked(context: ExerciseAudioControlContext): void {
    if (context.exerciseState !== 'exercise') {
      return;
    }

    context.onSaveExerciseState();
  }

  private handlePlayPauseShortcut(event: KeyboardEvent, context: ExerciseAudioControlContext): void {
    const isPlaying = context.audio?.isAudioPlaying() ?? false;
    event.preventDefault();

    this.exerciseSessionTracking.trackEvent('audio_shortcut_used', {
      shortcut: 'ctrl_or_cmd_enter',
      action: isPlaying ? 'pause' : 'play',
    });

    if (isPlaying) {
      this.pauseAudioWithTimerClear(context.audio);
      return;
    }

    this.markNextAudioPlaySource('keyboard_shortcut');
    this.playAudioWithAutoPause(context);
    context.onFinishExerciseTour();
  }

  private handleSeekShortcut(
    event: KeyboardEvent,
    context: ExerciseAudioControlContext,
    action: 'rewind' | 'forward',
  ): void {
    const seekSeconds = this.getShortcutSeekSeconds(context.section, action);
    event.preventDefault();

    this.exerciseSessionTracking.trackEvent('audio_shortcut_used', {
      shortcut: action === 'rewind' ? 'ctrl_or_cmd_arrow_left' : 'ctrl_or_cmd_arrow_right',
      action,
    }, {
      seek_seconds: seekSeconds,
    });

    if (action === 'rewind') {
      context.audio?.rewindAudio(seekSeconds);
      return;
    }

    context.audio?.forwardAudio(seekSeconds);
  }

  private getShortcutSeekSeconds(
    section: ExerciseAudioSectionControl | null,
    action: 'rewind' | 'forward',
  ): number {
    const selectedAutoPause = section?.selectedAutoPause?.() ?? 0;
    const forwardSeconds = selectedAutoPause > 0 ? selectedAutoPause : 2;
    return action === 'rewind' ? forwardSeconds + 1 : forwardSeconds;
  }

  private markNextAudioPlaySource(source: models.AudioPlaySource): void {
    this.clearPendingAudioPlaySource();
    this.pendingAudioPlaySource = source;
    this.pendingAudioPlaySourceTimer = setTimeout(() => {
      this.pendingAudioPlaySource = null;
      this.pendingAudioPlaySourceTimer = null;
    }, 2000);
  }

  private consumePendingAudioPlaySource(): models.AudioPlaySource {
    const source = this.pendingAudioPlaySource ?? 'manual_click';
    this.clearPendingAudioPlaySource();
    return source;
  }

  private clearPendingAudioPlaySource(): void {
    if (this.pendingAudioPlaySourceTimer) {
      clearTimeout(this.pendingAudioPlaySourceTimer);
      this.pendingAudioPlaySourceTimer = null;
    }

    this.pendingAudioPlaySource = null;
  }
}

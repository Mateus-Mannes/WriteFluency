import { TextComparisonResult } from 'src/api/listen-and-write';

export type ExerciseState = 'intro' | 'exercise' | 'results';

export type GtagEvent = (command: 'event', eventName: string, params: Record<string, unknown>) => void;
export type AudioPlaySource = 'manual_click' | 'keyboard_shortcut' | 'listen_first_prompt';
export type GuestLoginModalDismissReason = 'continue_as_guest' | 'backdrop';

export interface BeginExerciseContext {
  isFirstTimeUser: boolean;
  audioEndedBeforeBegin: boolean;
  guestBeginAttemptCount: number | null;
  guestLoginModalShownBeforeStart: boolean;
}

export interface GuestLoginModalDecision {
  shouldShow: boolean;
  reason: 'authenticated' | 'below_threshold' | 'cooldown_active' | 'eligible';
  cooldownRemainingMs: number;
}

export interface LocalExerciseSnapshot {
  state: ExerciseState | null;
  userText: string | null;
  autoPauseSeconds: number | null;
  pausedTimeSeconds: number | null;
  result: TextComparisonResult | null;
  savedAtUtc: string | null;
}

export interface RestoredExerciseSnapshot {
  state: ExerciseState | null;
  userText: string | null;
  autoPauseSeconds: number | null;
  pausedTimeSeconds: number | null;
  result: TextComparisonResult | null;
}

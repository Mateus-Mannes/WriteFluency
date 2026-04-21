export type ProgressStatus = 'in_progress' | 'completed' | 'disabled';

export interface StartProgressRequest {
  exerciseId: number;
  exerciseTitle?: string | null;
  subject?: string | null;
  complexity?: string | null;
  originalWordCount?: number | null;
}

export interface CompleteProgressRequest {
  exerciseId: number;
  accuracyPercentage?: number | null;
  wordCount?: number | null;
  originalWordCount?: number | null;
  exerciseTitle?: string | null;
  subject?: string | null;
  complexity?: string | null;
}

export interface SaveProgressStateRequest {
  exerciseId: number;
  exerciseState?: 'intro' | 'exercise' | 'results' | null;
  userText?: string | null;
  wordCount?: number | null;
  originalWordCount?: number | null;
  autoPauseSeconds?: number | null;
  pausedTimeSeconds?: number | null;
  exerciseTitle?: string | null;
  subject?: string | null;
  complexity?: string | null;
}

export interface ProgressOperationResponse {
  trackingEnabled: boolean;
  exerciseId: number;
  status: ProgressStatus;
  updatedAtUtc: string;
}

export interface ProgressStateResponse {
  trackingEnabled: boolean;
  exerciseId: number;
  hasServerState: boolean;
  exerciseState: 'intro' | 'exercise' | 'results' | null;
  userText: string | null;
  wordCount: number | null;
  autoPauseSeconds: number | null;
  pausedTimeSeconds: number | null;
  updatedAtUtc: string | null;
}

export interface ProgressSummaryResponse {
  trackingEnabled: boolean;
  totalItems: number;
  inProgressCount: number;
  completedCount: number;
  totalAttempts: number;
  totalActiveSeconds: number;
  averageAccuracyPercentage: number | null;
  bestAccuracyPercentage: number | null;
  lastActivityAtUtc: string | null;
}

export interface ProgressItemResponse {
  exerciseId: number;
  status: ProgressStatus;
  exerciseTitle: string | null;
  subject: string | null;
  complexity: string | null;
  attemptCount: number;
  latestAccuracyPercentage: number | null;
  bestAccuracyPercentage: number | null;
  activeSeconds: number;
  startedAtUtc: string;
  completedAtUtc: string | null;
  updatedAtUtc: string;
  currentWordCount?: number | null;
  originalWordCount?: number | null;
}

export interface ProgressAttemptResponse {
  attemptId: string;
  exerciseId: number;
  accuracyPercentage: number | null;
  wordCount: number | null;
  originalWordCount: number | null;
  activeSeconds: number;
  createdAtUtc: string;
  exerciseTitle: string | null;
  subject: string | null;
  complexity: string | null;
}

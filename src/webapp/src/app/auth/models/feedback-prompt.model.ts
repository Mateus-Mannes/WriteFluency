export interface FeedbackPromptStatusResponse {
  campaignKey: string;
  isEligible: boolean;
  nextEligibleAtUtc: string | null;
  lastShownAtUtc: string | null;
  lastDismissedAtUtc: string | null;
  lastSubmittedAtUtc: string | null;
  dismissCount: number;
  submitCount: number;
}

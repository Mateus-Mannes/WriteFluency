namespace WriteFluency.TextComparisons;

public enum ProReviewDecisionKind
{
    NoReview,
    FullProReview,
    LockedLogin,
    LockedUpgrade,
    UsageLimit
}

public sealed record ProReviewEligibilityDecision(
    ProReviewDecisionKind Kind,
    string MistakePatternStatus,
    string? MistakePatternMessage,
    string MistakePatternReviewSource,
    AiUsageReservation? Reservation,
    string ReasonCode)
{
    public static ProReviewEligibilityDecision NoReview(string reasonCode) =>
        new(
            ProReviewDecisionKind.NoReview,
            MistakePatternStatuses.NotApplicable,
            null,
            MistakePatternReviewSources.None,
            null,
            reasonCode);

    public static ProReviewEligibilityDecision FullProReview(
        AiUsageReservation reservation,
        string mistakePatternReviewSource,
        string reasonCode) =>
        new(
            ProReviewDecisionKind.FullProReview,
            MistakePatternStatuses.Generated,
            null,
            mistakePatternReviewSource,
            reservation,
            reasonCode);

    public static ProReviewEligibilityDecision LockedLogin(string reasonCode) =>
        new(
            ProReviewDecisionKind.LockedLogin,
            MistakePatternStatuses.LoginRequiredToUnlockReview,
            "Log in to unlock your free Pro review. Your correction highlights are still available.",
            MistakePatternReviewSources.None,
            null,
            reasonCode);

    public static ProReviewEligibilityDecision LockedUpgrade(string reasonCode) =>
        new(
            ProReviewDecisionKind.LockedUpgrade,
            MistakePatternStatuses.UpgradeRequiredToUnlockReview,
            "Upgrade to Pro to unlock mistake-pattern review for every attempt.",
            MistakePatternReviewSources.None,
            null,
            reasonCode);

    public static ProReviewEligibilityDecision UsageLimit(
        string? message,
        string reasonCode) =>
        new(
            ProReviewDecisionKind.UsageLimit,
            MistakePatternStatuses.SkippedUsageLimit,
            message,
            MistakePatternReviewSources.None,
            null,
            reasonCode);
}

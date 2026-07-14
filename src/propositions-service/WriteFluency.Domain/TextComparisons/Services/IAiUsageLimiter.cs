namespace WriteFluency.TextComparisons;

public interface IAiUsageLimiter
{
    Task<AiUsageReservation> TryReserveAsync(
        AiUsageReservationRequest request,
        CancellationToken cancellationToken);

    Task RecordCompletionAsync(
        AiUsageReservation reservation,
        AiUsageCompletion completion,
        CancellationToken cancellationToken);

    Task RecordFailureAsync(
        AiUsageReservation reservation,
        CancellationToken cancellationToken);
}

public sealed record AiUsageReservationRequest(
    string UserId,
    string Feature,
    AiUsageLimitPolicy? Policy = null);

public sealed record AiUsageLimitPolicy(
    int? DailySubmissionLimit = null,
    int? MonthlySubmissionLimit = null,
    int? LifetimeSubmissionLimit = null,
    decimal? MonthlyEstimatedCostLimitUsd = null);

public sealed record AiUsageReservationPeriod(
    string PeriodKind,
    string PeriodKey);

public sealed record AiUsageCompletion(
    long? InputTokenCount,
    long? OutputTokenCount);

public sealed record AiUsageReservation(
    bool IsAllowed,
    string? DenialReason,
    string UserId,
    string Feature,
    IReadOnlyList<AiUsageReservationPeriod> Periods)
{
    public string DailyPeriodKey => GetPeriodKey(AiUsagePeriodKinds.Day);
    public string MonthlyPeriodKey => GetPeriodKey(AiUsagePeriodKinds.Month);
    public string LifetimePeriodKey => GetPeriodKey(AiUsagePeriodKinds.Lifetime);

    public static AiUsageReservation Denied(
        string reason,
        string userId,
        string feature,
        IReadOnlyList<AiUsageReservationPeriod> periods) =>
        new(false, reason, userId, feature, periods);

    public static AiUsageReservation Allowed(
        string userId,
        string feature,
        IReadOnlyList<AiUsageReservationPeriod> periods) =>
        new(true, null, userId, feature, periods);

    public static AiUsageReservation Denied(
        string reason,
        string userId,
        string feature,
        string dailyPeriodKey,
        string monthlyPeriodKey) =>
        Denied(
            reason,
            userId,
            feature,
            [
                new AiUsageReservationPeriod(AiUsagePeriodKinds.Day, dailyPeriodKey),
                new AiUsageReservationPeriod(AiUsagePeriodKinds.Month, monthlyPeriodKey)
            ]);

    public static AiUsageReservation Allowed(
        string userId,
        string feature,
        string dailyPeriodKey,
        string monthlyPeriodKey) =>
        Allowed(
            userId,
            feature,
            [
                new AiUsageReservationPeriod(AiUsagePeriodKinds.Day, dailyPeriodKey),
                new AiUsageReservationPeriod(AiUsagePeriodKinds.Month, monthlyPeriodKey)
            ]);

    private string GetPeriodKey(string periodKind) =>
        Periods.FirstOrDefault(period =>
            string.Equals(period.PeriodKind, periodKind, StringComparison.Ordinal))?.PeriodKey
        ?? string.Empty;
}

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
    string Feature);

public sealed record AiUsageCompletion(
    long? InputTokenCount,
    long? OutputTokenCount);

public sealed record AiUsageReservation(
    bool IsAllowed,
    string? DenialReason,
    string UserId,
    string Feature,
    string DailyPeriodKey,
    string MonthlyPeriodKey)
{
    public static AiUsageReservation Denied(
        string reason,
        string userId,
        string feature,
        string dailyPeriodKey,
        string monthlyPeriodKey) =>
        new(false, reason, userId, feature, dailyPeriodKey, monthlyPeriodKey);

    public static AiUsageReservation Allowed(
        string userId,
        string feature,
        string dailyPeriodKey,
        string monthlyPeriodKey) =>
        new(true, null, userId, feature, dailyPeriodKey, monthlyPeriodKey);
}


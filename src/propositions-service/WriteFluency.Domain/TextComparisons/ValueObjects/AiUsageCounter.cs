namespace WriteFluency.TextComparisons;

public sealed class AiUsageCounter
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public string? AnonymousClientIpAddress { get; set; }
    public required string Feature { get; set; }
    public required string PeriodKind { get; set; }
    public required string PeriodKey { get; set; }
    public int ReservedRequestCount { get; set; }
    public int CompletedRequestCount { get; set; }
    public int FailedRequestCount { get; set; }
    public long InputTokenCount { get; set; }
    public long OutputTokenCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

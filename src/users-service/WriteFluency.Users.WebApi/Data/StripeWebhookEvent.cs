namespace WriteFluency.Users.WebApi.Data;

public sealed class StripeWebhookEvent
{
    public string StripeEventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public string ProcessingStatus { get; set; } = StripeWebhookEventStatuses.Processing;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}

public static class StripeWebhookEventStatuses
{
    public const string Processing = "processing";
    public const string Processed = "processed";
    public const string Ignored = "ignored";
    public const string Failed = "failed";
}

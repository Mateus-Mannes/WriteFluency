namespace WriteFluency.Propositions;

public sealed class CatalogAccessCounter
{
    public int Id { get; set; }
    public required string SubjectType { get; set; }
    public required string SubjectKey { get; set; }
    public string? AnonymousClientIpAddress { get; set; }
    public required string Feature { get; set; }
    public int UsedCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

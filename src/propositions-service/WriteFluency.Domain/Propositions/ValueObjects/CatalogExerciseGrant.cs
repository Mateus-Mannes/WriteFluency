namespace WriteFluency.Propositions;

public sealed class CatalogExerciseGrant
{
    public int Id { get; set; }
    public required string SubjectType { get; set; }
    public required string SubjectKey { get; set; }
    public int PropositionId { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

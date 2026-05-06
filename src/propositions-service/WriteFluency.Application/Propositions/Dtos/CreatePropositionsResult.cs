namespace WriteFluency.Propositions;

public sealed record CreatePropositionsResult(
    IReadOnlyList<Proposition> Propositions,
    DateTime? OldestFetchedPublishedOn,
    int FetchedCount)
{
    public static CreatePropositionsResult Empty { get; } = new(Array.Empty<Proposition>(), null, 0);
}

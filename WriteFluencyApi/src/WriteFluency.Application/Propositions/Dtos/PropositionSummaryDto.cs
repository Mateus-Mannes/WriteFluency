namespace WriteFluency.Propositions;

public class PropositionSummaryDto
{
    public required SubjectEnum SubjectId { get; set; }
    public required int Count { get; set; }
    public Proposition? NewestProposition { get; set; }
}

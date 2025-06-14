namespace WriteFluency.Propositions;

public class PropositionSummaryDto
{
    public required SubjectEnum SubjectId { get; set; }
    public required ComplexityEnum ComplexityId { get; set; }
    public DateTime? OldestPublishedOn { get; set; }
    public required int Count { get; set; }
}

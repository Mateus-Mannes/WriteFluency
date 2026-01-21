namespace WriteFluency.Propositions;

/// <summary>
/// Tracks proposition generation attempts to prevent duplicates and manage generation history
/// </summary>
public class PropositionGenerationLog
{
    public int Id { get; set; }

    public required DateTime GenerationDate { get; set; }
    public required SubjectEnum SubjectId { get; set; }
    public required ComplexityEnum ComplexityId { get; set; }
    
    /// <summary>
    /// Number of propositions successfully generated for this combination
    /// </summary>
    public required int SuccessCount { get; set; }
    
    /// <summary>
    /// Whether the generation was successful
    /// </summary>
    public required bool Success { get; set; }
    
    public required DateTime CreatedAt { get; set; }

    public Complexity? Complexity { get; set; }
    public Subject? Subject { get; set; }
    public ICollection<Proposition>? Propositions { get; set; }
}

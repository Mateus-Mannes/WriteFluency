namespace WriteFluency.Propositions;

public record ExerciseComparisonAccessResult(
    bool IsGranted,
    PropositionMetadataDto Metadata,
    string? OriginalText);

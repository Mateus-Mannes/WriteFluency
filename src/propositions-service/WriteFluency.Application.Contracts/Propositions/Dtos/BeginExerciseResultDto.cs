namespace WriteFluency.Propositions;

public record BeginExerciseResultDto(
    string Access,
    string? AudioUrl,
    DateTimeOffset? AudioExpiresAtUtc,
    PropositionMetadataDto Metadata);

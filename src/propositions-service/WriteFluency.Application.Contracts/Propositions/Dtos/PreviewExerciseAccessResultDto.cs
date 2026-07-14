namespace WriteFluency.Propositions;

public record PreviewExerciseAccessResultDto(
    string AccessStatus,
    string? AudioUrl,
    DateTimeOffset? AudioExpiresAtUtc,
    PropositionMetadataDto Metadata);

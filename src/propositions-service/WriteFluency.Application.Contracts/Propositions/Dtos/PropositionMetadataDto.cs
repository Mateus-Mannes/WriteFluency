namespace WriteFluency.Propositions;

public record PropositionMetadataDto(
    int Id,
    DateTime PublishedOn,
    SubjectEnum SubjectId,
    ComplexityEnum ComplexityId,
    int AudioDurationSeconds,
    string Title,
    string? ImageFileId,
    string? NewsUrl,
    bool RequiresPro);

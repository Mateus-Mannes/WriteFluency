namespace WriteFluency.Propositions;

public record ExerciseListItemDto(
    int Id,
    string Title,
    SubjectEnum Topic,
    ComplexityEnum Level,
    DateTime PublishedOn,
    string? ImageFileId,
    int AudioDurationSeconds
);

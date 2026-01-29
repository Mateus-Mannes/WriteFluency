namespace WriteFluency.Propositions;

public record ExerciseFilterDto(
    SubjectEnum? Topic = null,
    ComplexityEnum? Level = null,
    int PageNumber = 1,
    int PageSize = 9,
    string SortBy = "newest"
);

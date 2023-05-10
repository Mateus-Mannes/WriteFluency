using WriteFluencyApi.Shared.ListenAndWrite;

namespace WriteFluencyApi.Dtos.ListenAndWrite;

public record GeneratePropositionDto(
    ComplexityEnum Complexity,
    SubjectEnum Subject
);

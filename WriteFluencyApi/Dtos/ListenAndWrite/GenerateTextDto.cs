
using WriteFluencyApi.Shared.ListenAndWrite;

namespace WriteFluencyApi.Dtos.ListenAndWrite;

public record GenerateTextDto(
    ComplexityEnum Complexity,
    SubjectEnum Subject
);

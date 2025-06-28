namespace WriteFluency.Propositions;

public record CreatePropositionDto(
    DateTime PublishedOn,
    ComplexityEnum Complexity,
    SubjectEnum Subject
);

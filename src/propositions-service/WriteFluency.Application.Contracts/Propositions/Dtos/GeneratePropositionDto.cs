namespace WriteFluency.Propositions;

public record GetPropositionDto(
    ComplexityEnum Complexity,
    SubjectEnum Subject,
    List<int> AlreadyGeneratedIds
);

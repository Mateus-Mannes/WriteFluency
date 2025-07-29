namespace WriteFluency.Propositions;

public record NewsDto(
    string ExternalId,
    string Title,
    string Description,
    string Url,
    string ImageUrl,
    SubjectEnum Subject,
    DateTime PublishedOn
);

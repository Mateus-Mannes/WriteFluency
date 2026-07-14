using System.ComponentModel.DataAnnotations;

namespace WriteFluency.TextComparisons;

public record CompareTextsDto(
    int PropositionId,
    [property: MaxLength(3000)] string? UserText);

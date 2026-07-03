namespace WriteFluency.TextComparisons;

public sealed record MistakePatternClassificationRequest(
    string OriginalText,
    string UserText,
    IReadOnlyList<TextComparison> Comparisons);

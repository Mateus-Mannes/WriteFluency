namespace WriteFluency.TextComparisons;

public sealed record AiRefinementRequest(
    string OriginalText,
    string UserText,
    IReadOnlyList<AiRefinementSourceComparison> Comparisons);

public sealed record AiRefinementSourceComparison(
    int SourceComparisonIndex,
    TextRange OriginalTextRange,
    string OriginalText,
    TextRange UserTextRange,
    string UserText);

public sealed record AiRefinedComparison(
    int SourceComparisonIndex,
    int OriginalTextInitialIndex,
    int OriginalTextFinalIndex,
    int UserTextInitialIndex,
    int UserTextFinalIndex);

public sealed record AiRefinementResult(
    IReadOnlyList<AiRefinedComparison> Comparisons,
    string Model,
    string PromptVersion,
    long DurationMilliseconds,
    long? InputTokenCount = null,
    long? OutputTokenCount = null);

public sealed record AiRefinementValidationResult(
    bool IsValid,
    IReadOnlyList<TextComparison> Comparisons,
    string? FailureReason)
{
    public static AiRefinementValidationResult Success(IReadOnlyList<TextComparison> comparisons) =>
        new(true, comparisons, null);

    public static AiRefinementValidationResult Failure(string reason) =>
        new(false, [], reason);
}

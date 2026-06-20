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

public static class AiRefinementActions
{
    public const string Keep = "keep";
    public const string Remove = "remove";
    public const string Refine = "refine";
}

public sealed record AiRefinementDecision(
    int SourceComparisonIndex,
    string Action,
    string ReasonCode,
    IReadOnlyList<AiRefinedComparison> Comparisons);

public sealed record AiRefinementResult(
    IReadOnlyList<AiRefinementDecision> Decisions,
    string Model,
    string PromptVersion,
    long DurationMilliseconds,
    long? InputTokenCount = null,
    long? OutputTokenCount = null)
{
    public IReadOnlyList<AiRefinedComparison> Comparisons =>
        Decisions.SelectMany(decision => decision.Comparisons).ToList();
}

public sealed record AiRefinementValidationIssue(
    int SourceComparisonIndex,
    string Reason);

public sealed record AiRefinementValidationResult(
    bool IsValid,
    IReadOnlyList<TextComparison> Comparisons,
    IReadOnlyList<AiRefinedComparison> NormalizedRanges,
    string? FailureReason,
    IReadOnlyList<AiRefinementValidationIssue> RejectedSources)
{
    public int RejectedSourceComparisonCount => RejectedSources.Count;

    public static AiRefinementValidationResult Success(
        IReadOnlyList<TextComparison> comparisons,
        IReadOnlyList<AiRefinedComparison> normalizedRanges,
        IReadOnlyList<AiRefinementValidationIssue>? rejectedSources = null)
    {
        var issues = rejectedSources ?? [];
        return new(
            true,
            comparisons,
            normalizedRanges,
            issues.FirstOrDefault()?.Reason,
            issues);
    }

    public static AiRefinementValidationResult Failure(string reason) =>
        new(false, [], [], reason, []);
}

public sealed record AiRefinementDecisionValidation(
    int SourceComparisonIndex,
    string Action,
    string ReasonCode,
    IReadOnlyList<AiRefinedComparison> ProposedRanges,
    IReadOnlyList<TextComparison> OutputComparisons,
    string ValidationStatus,
    string? ValidationFailureReason)
{
    public bool IsEffectiveChange { get; init; }
}

public sealed record AiRefinementDecisionValidationResult(
    bool IsValid,
    IReadOnlyList<TextComparison> Comparisons,
    IReadOnlyList<AiRefinementDecisionValidation> Decisions,
    string? FailureReason,
    IReadOnlyList<AiRefinementValidationIssue> RejectedSources)
{
    public int RejectedSourceComparisonCount => RejectedSources.Count;

    public static AiRefinementDecisionValidationResult Failure(string reason) =>
        new(false, [], [], reason, []);
}

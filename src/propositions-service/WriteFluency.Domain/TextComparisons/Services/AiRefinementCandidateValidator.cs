namespace WriteFluency.TextComparisons;

internal sealed class AiRefinementCandidateValidator
{
    private readonly AiRefinementRangeBoundaryTrimmer _boundaryTrimmer = new();
    private readonly OneSidedInsertionRepairer _insertionRepairer = new();
    private readonly AiRefinementRangeOverflowNormalizer _overflowNormalizer =
        new();

    public AiRefinementCandidateValidationResult Validate(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        AiRefinedComparison candidate)
    {
        var originalCandidate = new TextRange(
            candidate.OriginalTextInitialIndex,
            candidate.OriginalTextFinalIndex);
        var userCandidate = new TextRange(
            candidate.UserTextInitialIndex,
            candidate.UserTextFinalIndex);

        if (!TextRangeNavigator.IsValid(
                originalCandidate,
                request.OriginalText.Length)
            || !TextRangeNavigator.IsValid(
                userCandidate,
                request.UserText.Length))
        {
            return AiRefinementCandidateValidationResult.Failure(
                AiRefinementValidationErrors.InvalidRange);
        }

        if (!_overflowNormalizer.TryNormalize(
                originalCandidate,
                source.OriginalTextRange,
                request.OriginalText,
                out var originalRange)
            || !_overflowNormalizer.TryNormalize(
                userCandidate,
                source.UserTextRange,
                request.UserText,
                out var userRange))
        {
            return AiRefinementCandidateValidationResult.Failure(
                AiRefinementValidationErrors.RangeOutsideSource);
        }

        if (!TextRangeNavigator.TryNormalizeBoundaries(
                request.OriginalText,
                source.OriginalTextRange,
                originalRange,
                out originalRange)
            || !TextRangeNavigator.TryNormalizeBoundaries(
                request.UserText,
                source.UserTextRange,
                userRange,
                out userRange))
        {
            return AiRefinementCandidateValidationResult.Failure(
                AiRefinementValidationErrors.EmptyRangeAfterNormalization);
        }

        _boundaryTrimmer.Trim(
            request,
            source,
            ref originalRange,
            ref userRange);

        if (!TryReadSnippets(
                request,
                originalRange,
                userRange,
                out var originalSnippet,
                out var userSnippet))
        {
            return AiRefinementCandidateValidationResult.Failure(
                AiRefinementValidationErrors.UnsafeTextSlice);
        }

        if (!TextRangeNavigator.HasCompleteWordBoundaries(
                request.OriginalText,
                originalRange)
            || !TextRangeNavigator.HasCompleteWordBoundaries(
                request.UserText,
                userRange))
        {
            return AiRefinementCandidateValidationResult.Failure(
                AiRefinementValidationErrors.PartialWordRange);
        }

        if (AreEqualAfterTrimAndCase(originalSnippet, userSnippet)
            && (!_insertionRepairer.TryRepair(
                    request,
                    source,
                    originalRange,
                    userRange,
                    out originalRange,
                    out userRange)
                || !TryReadSnippets(
                    request,
                    originalRange,
                    userRange,
                    out originalSnippet,
                    out userSnippet)
                || AreEqualAfterTrimAndCase(
                    originalSnippet,
                    userSnippet)))
        {
            return AiRefinementCandidateValidationResult.Failure(
                AiRefinementValidationErrors.IdenticalSelectedText);
        }

        return AiRefinementCandidateValidationResult.Success(
            new AiRefinedComparison(
                candidate.SourceComparisonIndex,
                originalRange.InitialIndex,
                originalRange.FinalIndex,
                userRange.InitialIndex,
                userRange.FinalIndex));
    }

    private static bool TryReadSnippets(
        AiRefinementRequest request,
        TextRange originalRange,
        TextRange userRange,
        out string originalSnippet,
        out string userSnippet)
    {
        userSnippet = string.Empty;
        return TextRangeNavigator.TrySlice(
            request.OriginalText,
            originalRange,
            out originalSnippet)
            && TextRangeNavigator.TrySlice(
                request.UserText,
                userRange,
                out userSnippet);
    }

    private static bool AreEqualAfterTrimAndCase(
        string originalText,
        string userText) =>
        string.Equals(
            originalText.Trim(),
            userText.Trim(),
            StringComparison.OrdinalIgnoreCase);
}

internal sealed record AiRefinementCandidateValidationResult(
    bool IsValid,
    AiRefinedComparison? Range,
    string? FailureReason)
{
    public static AiRefinementCandidateValidationResult Success(
        AiRefinedComparison range) =>
        new(true, range, null);

    public static AiRefinementCandidateValidationResult Failure(
        string reason) =>
        new(false, null, reason);
}

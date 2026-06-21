namespace WriteFluency.TextComparisons;

internal sealed class AiRefinementRangeValidator
{
    private readonly AiRefinementRangeCanonicalizer _canonicalizer = new();
    private readonly OneSidedInsertionRepairer _insertionRepairer = new();

    public AiRefinementValidationResult Validate(
        AiRefinementRequest request,
        IReadOnlyList<AiRefinedComparison>? refinedComparisons)
    {
        if (refinedComparisons is null)
        {
            return AiRefinementValidationResult.Failure(
                AiRefinementValidationErrors.MissingComparisons);
        }

        var sources = request.Comparisons.ToDictionary(
            comparison => comparison.SourceComparisonIndex);
        if (refinedComparisons.Any(candidate =>
                !sources.ContainsKey(candidate.SourceComparisonIndex)))
        {
            return AiRefinementValidationResult.Failure(
                AiRefinementValidationErrors.UnknownSourceComparison);
        }

        var validatedEntries = new List<ValidatedEntry>(
            refinedComparisons.Count);
        var rejectedSources = new List<AiRefinementValidationIssue>();
        var acceptedCandidateCount = 0;

        foreach (var candidateGroup in refinedComparisons.GroupBy(
                     candidate => candidate.SourceComparisonIndex))
        {
            var source = sources[candidateGroup.Key];
            var result = ValidateSource(
                request,
                source,
                candidateGroup.ToList());

            if (!result.IsValid)
            {
                rejectedSources.Add(new AiRefinementValidationIssue(
                    source.SourceComparisonIndex,
                    result.FailureReason!));
                validatedEntries.Add(CreateSourceFallbackEntry(source));
                continue;
            }

            acceptedCandidateCount += result.Entries.Count;
            validatedEntries.AddRange(result.Entries);
        }

        var omittedSourceCount = sources.Keys.Except(
            refinedComparisons.Select(candidate =>
                candidate.SourceComparisonIndex)).Count();

        if (rejectedSources.Count > 0
            && acceptedCandidateCount == 0
            && omittedSourceCount == 0)
        {
            return AiRefinementValidationResult.Failure(
                rejectedSources[0].Reason);
        }

        var orderedResults = validatedEntries
            .OrderBy(result =>
                result.Comparison.OriginalTextRange.InitialIndex)
            .ThenBy(result =>
                result.Comparison.UserTextRange.InitialIndex)
            .ToList();

        return AiRefinementValidationResult.Success(
            orderedResults.Select(result => result.Comparison).ToList(),
            orderedResults.Select(result => result.Range).ToList(),
            rejectedSources);
    }

    private SourceValidationResult ValidateSource(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        IReadOnlyList<AiRefinedComparison> candidates)
    {
        if (_canonicalizer.HasCrossingRanges(candidates))
        {
            return SourceValidationResult.Failure(
                AiRefinementValidationErrors.CrossingRanges);
        }

        var entries = new List<ValidatedEntry>();
        var ignoredEquivalentCandidateCount = 0;

        foreach (var candidate in _canonicalizer.MergeAdjacent(
                     request,
                     candidates))
        {
            var result = ValidateCandidate(request, source, candidate);
            if (result.IsValid)
            {
                entries.Add(result.Entry!);
                continue;
            }

            if (result.FailureReason
                == AiRefinementValidationErrors.IdenticalSelectedText)
            {
                ignoredEquivalentCandidateCount++;
                continue;
            }

            return SourceValidationResult.Failure(result.FailureReason!);
        }

        return entries.Count == 0 && ignoredEquivalentCandidateCount > 0
            ? SourceValidationResult.Failure(
                AiRefinementValidationErrors.IdenticalSelectedText)
            : SourceValidationResult.Success(entries);
    }

    private CandidateValidationResult ValidateCandidate(
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
            return CandidateValidationResult.Failure(
                AiRefinementValidationErrors.InvalidRange);
        }

        if (!TextRangeNavigator.TryNormalizeToSource(
                originalCandidate,
                source.OriginalTextRange,
                request.OriginalText,
                out var originalRange)
            || !TextRangeNavigator.TryNormalizeToSource(
                userCandidate,
                source.UserTextRange,
                request.UserText,
                out var userRange))
        {
            return CandidateValidationResult.Failure(
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
            return CandidateValidationResult.Failure(
                AiRefinementValidationErrors.EmptyRangeAfterNormalization);
        }

        _canonicalizer.TrimMatchingBoundaryWords(
            request.OriginalText,
            request.UserText,
            ref originalRange,
            ref userRange);

        if (!TryReadSnippets(
                request,
                originalRange,
                userRange,
                out var originalSnippet,
                out var userSnippet))
        {
            return CandidateValidationResult.Failure(
                AiRefinementValidationErrors.UnsafeTextSlice);
        }

        if (!TextRangeNavigator.HasCompleteWordBoundaries(
                request.OriginalText,
                originalRange)
            || !TextRangeNavigator.HasCompleteWordBoundaries(
                request.UserText,
                userRange))
        {
            return CandidateValidationResult.Failure(
                AiRefinementValidationErrors.PartialWordRange);
        }

        if (AreEqualAfterTrimAndCase(originalSnippet, userSnippet))
        {
            if (!_insertionRepairer.TryRepair(
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
                    userSnippet))
            {
                return CandidateValidationResult.Failure(
                    AiRefinementValidationErrors.IdenticalSelectedText);
            }
        }

        return CandidateValidationResult.Success(
            new ValidatedEntry(
                new TextComparison(
                    originalRange,
                    originalSnippet,
                    userRange,
                    userSnippet,
                    candidate.SourceComparisonIndex,
                    isAiRefined: true),
                new AiRefinedComparison(
                    candidate.SourceComparisonIndex,
                    originalRange.InitialIndex,
                    originalRange.FinalIndex,
                    userRange.InitialIndex,
                    userRange.FinalIndex)));
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

    private static ValidatedEntry CreateSourceFallbackEntry(
        AiRefinementSourceComparison source) =>
        new(
            AiRefinementComparisonFactory.CreateSource(source),
            new AiRefinedComparison(
                source.SourceComparisonIndex,
                source.OriginalTextRange.InitialIndex,
                source.OriginalTextRange.FinalIndex,
                source.UserTextRange.InitialIndex,
                source.UserTextRange.FinalIndex));

    private sealed record ValidatedEntry(
        TextComparison Comparison,
        AiRefinedComparison Range);

    private sealed record CandidateValidationResult(
        bool IsValid,
        ValidatedEntry? Entry,
        string? FailureReason)
    {
        public static CandidateValidationResult Success(
            ValidatedEntry entry) =>
            new(true, entry, null);

        public static CandidateValidationResult Failure(string reason) =>
            new(false, null, reason);
    }

    private sealed record SourceValidationResult(
        bool IsValid,
        IReadOnlyList<ValidatedEntry> Entries,
        string? FailureReason)
    {
        public static SourceValidationResult Success(
            IReadOnlyList<ValidatedEntry> entries) =>
            new(true, entries, null);

        public static SourceValidationResult Failure(string reason) =>
            new(false, [], reason);
    }
}

internal static class AiRefinementComparisonFactory
{
    public static TextComparison CreateSource(
        AiRefinementSourceComparison source) =>
        new(
            source.OriginalTextRange,
            source.OriginalText,
            source.UserTextRange,
            source.UserText,
            source.SourceComparisonIndex);
}

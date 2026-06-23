namespace WriteFluency.TextComparisons;

internal sealed class AiRefinementRangeValidator
{
    private readonly AiRefinementCandidateValidator _candidateValidator = new();
    private readonly AiRefinementRangeCanonicalizer _canonicalizer = new();
    private readonly AiRefinementRangeShapeNormalizer _shapeNormalizer = new();

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
            var result = _candidateValidator.Validate(
                request,
                source,
                candidate);
            if (result.IsValid)
            {
                var entry = CreateValidatedEntry(request, result.Range!);
                if (entry is null)
                {
                    return SourceValidationResult.Failure(
                        AiRefinementValidationErrors.UnsafeTextSlice);
                }

                entries.Add(entry);
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

        if (entries.Count == 0 && ignoredEquivalentCandidateCount > 0)
        {
            return SourceValidationResult.Failure(
                AiRefinementValidationErrors.IdenticalSelectedText);
        }

        return SourceValidationResult.Success(
            CanonicalizeEntries(request, entries));
    }

    private IReadOnlyList<ValidatedEntry> CanonicalizeEntries(
        AiRefinementRequest request,
        IReadOnlyList<ValidatedEntry> entries)
    {
        if (entries.Count == 0)
        {
            return entries;
        }

        var canonical = new List<ValidatedEntry>();
        foreach (var range in _shapeNormalizer.Normalize(
                     request,
                     entries.Select(entry => entry.Range).ToList()))
        {
            var entry = CreateValidatedEntry(
                request,
                range.SourceComparisonIndex,
                new TextRange(
                    range.OriginalTextInitialIndex,
                    range.OriginalTextFinalIndex),
                new TextRange(
                    range.UserTextInitialIndex,
                    range.UserTextFinalIndex));
            if (entry is null)
            {
                return entries;
            }

            canonical.Add(entry);
        }

        return canonical;
    }

    private static ValidatedEntry? CreateValidatedEntry(
        AiRefinementRequest request,
        AiRefinedComparison range) =>
        CreateValidatedEntry(
            request,
            range.SourceComparisonIndex,
            new TextRange(
                range.OriginalTextInitialIndex,
                range.OriginalTextFinalIndex),
            new TextRange(
                range.UserTextInitialIndex,
                range.UserTextFinalIndex));

    private static ValidatedEntry? CreateValidatedEntry(
        AiRefinementRequest request,
        int sourceComparisonIndex,
        TextRange originalRange,
        TextRange userRange)
    {
        if (!TryReadSnippets(
                request,
                originalRange,
                userRange,
                out var originalSnippet,
                out var userSnippet))
        {
            return null;
        }

        return new ValidatedEntry(
            new TextComparison(
                originalRange,
                originalSnippet,
                userRange,
                userSnippet,
                sourceComparisonIndex,
                isAiRefined: true),
            new AiRefinedComparison(
                sourceComparisonIndex,
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

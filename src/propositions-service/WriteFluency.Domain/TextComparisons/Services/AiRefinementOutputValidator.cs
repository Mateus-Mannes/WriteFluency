namespace WriteFluency.TextComparisons;

public sealed class AiRefinementOutputValidator
{
    public AiRefinementValidationResult Validate(
        AiRefinementRequest request,
        IReadOnlyList<AiRefinedComparison>? refinedComparisons)
    {
        if (refinedComparisons is null)
        {
            return AiRefinementValidationResult.Failure("missing_comparisons");
        }

        var sources = request.Comparisons.ToDictionary(
            comparison => comparison.SourceComparisonIndex);

        foreach (var candidate in refinedComparisons)
        {
            if (!sources.ContainsKey(candidate.SourceComparisonIndex))
            {
                return AiRefinementValidationResult.Failure("unknown_source_comparison");
            }
        }

        var validatedEntries = new List<ValidatedEntry>(refinedComparisons.Count);
        var rejectedSources = new List<AiRefinementValidationIssue>();
        var acceptedCandidateCount = 0;

        foreach (var candidateGroup in refinedComparisons.GroupBy(
                     candidate => candidate.SourceComparisonIndex))
        {
            var source = sources[candidateGroup.Key];
            var sourceEntries = new List<ValidatedEntry>();
            string? sourceFailureReason = null;

            foreach (var candidate in candidateGroup)
            {
                if (!TryValidateCandidate(
                        request,
                        source,
                        candidate,
                        out var entry,
                        out sourceFailureReason))
                {
                    break;
                }

                sourceEntries.Add(entry);
            }

            if (sourceFailureReason is not null)
            {
                rejectedSources.Add(new AiRefinementValidationIssue(
                    source.SourceComparisonIndex,
                    sourceFailureReason));
                validatedEntries.Add(CreateSourceFallbackEntry(source));
                continue;
            }

            acceptedCandidateCount += sourceEntries.Count;
            validatedEntries.AddRange(sourceEntries);
        }

        var omittedSourceCount = sources.Keys.Except(
            refinedComparisons.Select(candidate => candidate.SourceComparisonIndex)).Count();

        if (rejectedSources.Count > 0
            && acceptedCandidateCount == 0
            && omittedSourceCount == 0)
        {
            return AiRefinementValidationResult.Failure(rejectedSources[0].Reason);
        }

        var orderedResults = validatedEntries
            .OrderBy(result => result.Comparison.OriginalTextRange.InitialIndex)
            .ThenBy(result => result.Comparison.UserTextRange.InitialIndex)
            .ToList();

        return AiRefinementValidationResult.Success(
            orderedResults.Select(result => result.Comparison).ToList(),
            orderedResults.Select(result => result.Range).ToList(),
            rejectedSources);
    }

    private static bool TryValidateCandidate(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        AiRefinedComparison candidate,
        out ValidatedEntry entry,
        out string? failureReason)
    {
        entry = default!;
        failureReason = null;

        var candidateOriginalRange = new TextRange(
            candidate.OriginalTextInitialIndex,
            candidate.OriginalTextFinalIndex);
        var candidateUserRange = new TextRange(
            candidate.UserTextInitialIndex,
            candidate.UserTextFinalIndex);

        if (!IsValidRange(candidateOriginalRange, request.OriginalText.Length)
            || !IsValidRange(candidateUserRange, request.UserText.Length))
        {
            failureReason = "invalid_range";
            return false;
        }

        if (!TryNormalizeToSource(
                candidateOriginalRange,
                source.OriginalTextRange,
                request.OriginalText,
                out var originalRange)
            || !TryNormalizeToSource(
                candidateUserRange,
                source.UserTextRange,
                request.UserText,
                out var userRange))
        {
            failureReason = "range_outside_source";
            return false;
        }

        if (!TryNormalizeBoundaries(
                request.OriginalText,
                source.OriginalTextRange,
                originalRange,
                out originalRange)
            || !TryNormalizeBoundaries(
                request.UserText,
                source.UserTextRange,
                userRange,
                out userRange))
        {
            failureReason = "empty_range_after_normalization";
            return false;
        }

        if (!TrySlice(request.OriginalText, originalRange, out var originalSnippet)
            || !TrySlice(request.UserText, userRange, out var userSnippet))
        {
            failureReason = "unsafe_text_slice";
            return false;
        }

        if (!HasCompleteWordBoundaries(request.OriginalText, originalRange)
            || !HasCompleteWordBoundaries(request.UserText, userRange))
        {
            failureReason = "partial_word_range";
            return false;
        }

        entry = new ValidatedEntry(
            new TextComparison(
                originalRange,
                originalSnippet,
                userRange,
                userSnippet),
            new AiRefinedComparison(
                candidate.SourceComparisonIndex,
                originalRange.InitialIndex,
                originalRange.FinalIndex,
                userRange.InitialIndex,
                userRange.FinalIndex));
        return true;
    }

    private static ValidatedEntry CreateSourceFallbackEntry(
        AiRefinementSourceComparison source) =>
        new(
            new TextComparison(
                source.OriginalTextRange,
                source.OriginalText,
                source.UserTextRange,
                source.UserText),
            new AiRefinedComparison(
                source.SourceComparisonIndex,
                source.OriginalTextRange.InitialIndex,
                source.OriginalTextRange.FinalIndex,
                source.UserTextRange.InitialIndex,
                source.UserTextRange.FinalIndex));

    private sealed record ValidatedEntry(
        TextComparison Comparison,
        AiRefinedComparison Range);

    private static bool IsValidRange(TextRange range, int textLength) =>
        textLength > 0
        && range.InitialIndex >= 0
        && range.FinalIndex >= range.InitialIndex
        && range.FinalIndex < textLength;

    private static bool IsContainedBy(TextRange candidate, TextRange source) =>
        candidate.InitialIndex >= source.InitialIndex
        && candidate.FinalIndex <= source.FinalIndex;

    private static bool TryNormalizeToSource(
        TextRange candidate,
        TextRange source,
        string fullText,
        out TextRange normalized)
    {
        normalized = candidate;
        if (IsContainedBy(candidate, source))
        {
            return true;
        }

        if (candidate.InitialIndex < source.InitialIndex
            && !ContainsOnlyIgnorableBoundaryCharacters(
                fullText,
                candidate.InitialIndex,
                Math.Min(candidate.FinalIndex, source.InitialIndex - 1)))
        {
            return false;
        }

        if (candidate.FinalIndex > source.FinalIndex
            && !ContainsOnlyIgnorableBoundaryCharacters(
                fullText,
                Math.Max(candidate.InitialIndex, source.FinalIndex + 1),
                candidate.FinalIndex))
        {
            return false;
        }

        normalized = new TextRange(
            Math.Max(candidate.InitialIndex, source.InitialIndex),
            Math.Min(candidate.FinalIndex, source.FinalIndex));

        return normalized.FinalIndex >= normalized.InitialIndex
            && IsContainedBy(normalized, source);
    }

    private static bool ContainsOnlyIgnorableBoundaryCharacters(
        string text,
        int initialIndex,
        int finalIndex)
    {
        if (finalIndex < initialIndex)
        {
            return true;
        }

        for (var index = initialIndex; index <= finalIndex; index++)
        {
            var character = text[index];
            if (!char.IsWhiteSpace(character)
                && !char.IsPunctuation(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryNormalizeBoundaries(
        string text,
        TextRange source,
        TextRange candidate,
        out TextRange normalized)
    {
        var initialIndex = candidate.InitialIndex;
        var finalIndex = candidate.FinalIndex;

        while (initialIndex > source.InitialIndex
               && IsWordCharacter(text[initialIndex])
               && IsWordCharacter(text[initialIndex - 1]))
        {
            initialIndex--;
        }

        while (finalIndex < source.FinalIndex
               && IsWordCharacter(text[finalIndex])
               && IsWordCharacter(text[finalIndex + 1]))
        {
            finalIndex++;
        }

        while (initialIndex <= finalIndex
               && IsIgnorableBoundaryCharacter(text[initialIndex]))
        {
            initialIndex++;
        }

        while (finalIndex >= initialIndex
               && IsIgnorableBoundaryCharacter(text[finalIndex]))
        {
            finalIndex--;
        }

        normalized = new TextRange(initialIndex, finalIndex);
        return initialIndex <= finalIndex
            && IsContainedBy(normalized, source);
    }

    private static bool IsWordCharacter(char character) =>
        char.IsLetterOrDigit(character);

    private static bool IsIgnorableBoundaryCharacter(char character) =>
        char.IsWhiteSpace(character)
        || char.IsPunctuation(character);

    private static bool TrySlice(string text, TextRange range, out string snippet)
    {
        try
        {
            snippet = text.Substring(
                range.InitialIndex,
                range.FinalIndex - range.InitialIndex + 1);
            return snippet.Length > 0;
        }
        catch (ArgumentOutOfRangeException)
        {
            snippet = string.Empty;
            return false;
        }
    }

    private static bool HasCompleteWordBoundaries(
        string text,
        TextRange range)
    {
        var startsInsideWord = range.InitialIndex > 0
            && IsWordCharacter(text[range.InitialIndex])
            && IsWordCharacter(text[range.InitialIndex - 1]);
        var endsInsideWord = range.FinalIndex < text.Length - 1
            && IsWordCharacter(text[range.FinalIndex])
            && IsWordCharacter(text[range.FinalIndex + 1]);

        return !startsInsideWord && !endsInsideWord;
    }
}

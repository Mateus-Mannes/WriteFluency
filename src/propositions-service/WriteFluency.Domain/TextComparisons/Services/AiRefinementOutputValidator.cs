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
        var validatedComparisons = new List<TextComparison>(refinedComparisons.Count);
        var normalizedRanges = new List<AiRefinedComparison>(refinedComparisons.Count);

        foreach (var candidate in refinedComparisons)
        {
            if (!sources.TryGetValue(candidate.SourceComparisonIndex, out var source))
            {
                return AiRefinementValidationResult.Failure("unknown_source_comparison");
            }

            var candidateOriginalRange = new TextRange(
                candidate.OriginalTextInitialIndex,
                candidate.OriginalTextFinalIndex);
            var candidateUserRange = new TextRange(
                candidate.UserTextInitialIndex,
                candidate.UserTextFinalIndex);

            if (!IsValidRange(candidateOriginalRange, request.OriginalText.Length)
                || !IsValidRange(candidateUserRange, request.UserText.Length))
            {
                return AiRefinementValidationResult.Failure("invalid_range");
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
                return AiRefinementValidationResult.Failure("range_outside_source");
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
                return AiRefinementValidationResult.Failure(
                    "empty_range_after_normalization");
            }

            if (!TrySlice(request.OriginalText, originalRange, out var originalSnippet)
                || !TrySlice(request.UserText, userRange, out var userSnippet))
            {
                return AiRefinementValidationResult.Failure("unsafe_text_slice");
            }

            if (!HasCompleteWordBoundaries(request.OriginalText, originalRange)
                || !HasCompleteWordBoundaries(request.UserText, userRange))
            {
                return AiRefinementValidationResult.Failure("partial_word_range");
            }

            validatedComparisons.Add(new TextComparison(
                originalRange,
                originalSnippet,
                userRange,
                userSnippet));
            normalizedRanges.Add(new AiRefinedComparison(
                candidate.SourceComparisonIndex,
                originalRange.InitialIndex,
                originalRange.FinalIndex,
                userRange.InitialIndex,
                userRange.FinalIndex));
        }

        var orderedResults = validatedComparisons
            .Zip(normalizedRanges)
            .OrderBy(result => result.First.OriginalTextRange.InitialIndex)
            .ThenBy(result => result.First.UserTextRange.InitialIndex)
            .ToList();

        return AiRefinementValidationResult.Success(
            orderedResults.Select(result => result.First).ToList(),
            orderedResults.Select(result => result.Second).ToList());
    }

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

namespace WriteFluency.TextComparisons;

internal sealed class AiRefinementRangeCanonicalizer
{
    public bool HasCrossingRanges(
        IReadOnlyList<AiRefinedComparison> candidates)
    {
        var ordered = candidates
            .OrderBy(candidate => candidate.OriginalTextInitialIndex)
            .ThenBy(candidate => candidate.OriginalTextFinalIndex)
            .ToList();

        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].UserTextInitialIndex
                < ordered[index - 1].UserTextInitialIndex)
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<AiRefinedComparison> MergeAdjacent(
        AiRefinementRequest request,
        IEnumerable<AiRefinedComparison> candidates)
    {
        var ordered = candidates
            .OrderBy(candidate => candidate.OriginalTextInitialIndex)
            .ThenBy(candidate => candidate.UserTextInitialIndex)
            .ToList();
        if (ordered.Count < 2)
        {
            return ordered;
        }

        var merged = new List<AiRefinedComparison>(ordered.Count);
        var current = ordered[0];
        foreach (var next in ordered.Skip(1))
        {
            if (CanMerge(request, current, next))
            {
                current = Merge(current, next);
                continue;
            }

            merged.Add(current);
            current = next;
        }

        merged.Add(current);
        return merged;
    }

    public void TrimMatchingBoundaryWords(
        string originalText,
        string userText,
        ref TextRange originalRange,
        ref TextRange userRange)
    {
        while (TextRangeNavigator.TryGetFirstWord(
                   originalText,
                   originalRange,
                   out var originalWord)
               && TextRangeNavigator.TryGetFirstWord(
                   userText,
                   userRange,
                   out var userWord)
               && TextRangeNavigator.AreMatchingWords(
                   originalText,
                   originalWord,
                   userText,
                   userWord)
               && CanRemoveWord(originalRange, originalWord)
               && CanRemoveWord(userRange, userWord))
        {
            originalRange = TextRangeNavigator.TrimLeadingWord(
                originalText,
                originalRange,
                originalWord);
            userRange = TextRangeNavigator.TrimLeadingWord(
                userText,
                userRange,
                userWord);
        }

        while (TextRangeNavigator.TryGetLastWord(
                   originalText,
                   originalRange,
                   out var originalWord)
               && TextRangeNavigator.TryGetLastWord(
                   userText,
                   userRange,
                   out var userWord)
               && TextRangeNavigator.AreMatchingWords(
                   originalText,
                   originalWord,
                   userText,
                   userWord)
               && CanRemoveWord(originalRange, originalWord)
               && CanRemoveWord(userRange, userWord))
        {
            originalRange = TextRangeNavigator.TrimTrailingWord(
                originalText,
                originalRange,
                originalWord);
            userRange = TextRangeNavigator.TrimTrailingWord(
                userText,
                userRange,
                userWord);
        }
    }

    private static bool CanMerge(
        AiRefinementRequest request,
        AiRefinedComparison current,
        AiRefinedComparison next)
    {
        if (current.SourceComparisonIndex != next.SourceComparisonIndex
            || !IsValid(current, request)
            || !IsValid(next, request)
            || next.OriginalTextInitialIndex < current.OriginalTextInitialIndex
            || next.UserTextInitialIndex < current.UserTextInitialIndex)
        {
            return false;
        }

        return TextRangeNavigator.ContainsOnlyIgnorableCharacters(
                   request.OriginalText,
                   current.OriginalTextFinalIndex + 1,
                   next.OriginalTextInitialIndex - 1)
               && TextRangeNavigator.ContainsOnlyIgnorableCharacters(
                   request.UserText,
                   current.UserTextFinalIndex + 1,
                   next.UserTextInitialIndex - 1);
    }

    private static bool IsValid(
        AiRefinedComparison candidate,
        AiRefinementRequest request) =>
        TextRangeNavigator.IsValid(
            new TextRange(
                candidate.OriginalTextInitialIndex,
                candidate.OriginalTextFinalIndex),
            request.OriginalText.Length)
        && TextRangeNavigator.IsValid(
            new TextRange(
                candidate.UserTextInitialIndex,
                candidate.UserTextFinalIndex),
            request.UserText.Length);

    private static AiRefinedComparison Merge(
        AiRefinedComparison current,
        AiRefinedComparison next) =>
        new(
            current.SourceComparisonIndex,
            Math.Min(
                current.OriginalTextInitialIndex,
                next.OriginalTextInitialIndex),
            Math.Max(
                current.OriginalTextFinalIndex,
                next.OriginalTextFinalIndex),
            Math.Min(
                current.UserTextInitialIndex,
                next.UserTextInitialIndex),
            Math.Max(
                current.UserTextFinalIndex,
                next.UserTextFinalIndex));

    private static bool CanRemoveWord(TextRange range, TextRange word) =>
        word.InitialIndex > range.InitialIndex
        || word.FinalIndex < range.FinalIndex;
}

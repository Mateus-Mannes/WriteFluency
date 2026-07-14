namespace WriteFluency.TextComparisons;

public static class TextComparisonStructureGuard
{
    public static void EnsureValid(
        TextComparisonResult result)
    {
        var previousOriginalEnd = -1;
        var previousUserEnd = -1;

        for (var index = 0; index < result.Comparisons.Count; index++)
        {
            var comparison = result.Comparisons[index];
            EnsureRangeIsValid(
                result.OriginalText,
                comparison.OriginalTextRange,
                index,
                "original");
            EnsureRangeIsValid(
                result.UserText,
                comparison.UserTextRange,
                index,
                "user");
            EnsureSnippetMatches(
                result.OriginalText,
                comparison.OriginalTextRange,
                comparison.OriginalText,
                index,
                "original");
            EnsureSnippetMatches(
                result.UserText,
                comparison.UserTextRange,
                comparison.UserText,
                index,
                "user");

            if (comparison.OriginalTextRange.InitialIndex <= previousOriginalEnd)
            {
                throw CreateException(index, "original ranges must be sorted and non-overlapping");
            }

            if (comparison.UserTextRange.InitialIndex <= previousUserEnd)
            {
                throw CreateException(index, "user ranges must be monotonic and non-overlapping");
            }

            previousOriginalEnd = comparison.OriginalTextRange.FinalIndex;
            previousUserEnd = comparison.UserTextRange.FinalIndex;
        }
    }

    public static void EnsureValidSourceIndexes(
        IReadOnlyList<TextComparison> comparisons)
    {
        for (var index = 0; index < comparisons.Count; index++)
        {
            if (comparisons[index].SourceComparisonIndex < 0)
            {
                throw CreateException(index, "source comparison index is negative");
            }
        }
    }

    private static void EnsureRangeIsValid(
        string text,
        TextRange range,
        int comparisonIndex,
        string side)
    {
        if (range.InitialIndex < 0)
        {
            throw CreateException(comparisonIndex, $"{side} range initial index is negative");
        }

        if (range.FinalIndex < range.InitialIndex)
        {
            throw CreateException(comparisonIndex, $"{side} range final index is before initial index");
        }

        if (text.Length == 0 || range.FinalIndex >= text.Length)
        {
            throw CreateException(comparisonIndex, $"{side} range is out of bounds");
        }
    }

    private static void EnsureSnippetMatches(
        string text,
        TextRange range,
        string? selectedText,
        int comparisonIndex,
        string side)
    {
        var expected = TextRangeNavigator.Slice(text, range);
        if (selectedText != expected)
        {
            throw CreateException(comparisonIndex, $"{side} selected text does not match its range");
        }
    }

    private static InvalidOperationException CreateException(
        int comparisonIndex,
        string reason) =>
        new($"Invalid text comparison structure at comparison {comparisonIndex}: {reason}.");
}

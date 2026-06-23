namespace WriteFluency.TextComparisons;

internal sealed class AiRefinementRangeShapeNormalizer
{
    private readonly AiRefinementRangeSegmenter _segmenter = new();

    public IReadOnlyList<AiRefinedComparison> Normalize(
        AiRefinementRequest request,
        IReadOnlyList<AiRefinedComparison> ranges)
    {
        if (ranges.Count == 0)
        {
            return ranges;
        }

        var sourceComparisonIndex = ranges[0].SourceComparisonIndex;
        var originalRange = new TextRange(
            ranges.Min(range => range.OriginalTextInitialIndex),
            ranges.Max(range => range.OriginalTextFinalIndex));
        var userRange = new TextRange(
            ranges.Min(range => range.UserTextInitialIndex),
            ranges.Max(range => range.UserTextFinalIndex));

        return _segmenter
            .Split(
                request.OriginalText,
                request.UserText,
                originalRange,
                userRange,
                preserveExistingSplit: ranges.Count > 1)
            .Select(segment => new AiRefinedComparison(
                sourceComparisonIndex,
                segment.Original.InitialIndex,
                segment.Original.FinalIndex,
                segment.User.InitialIndex,
                segment.User.FinalIndex))
            .ToList();
    }
}

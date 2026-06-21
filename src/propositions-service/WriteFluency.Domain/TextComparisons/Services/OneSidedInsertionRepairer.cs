namespace WriteFluency.TextComparisons;

internal sealed class OneSidedInsertionRepairer
{
    public bool TryRepair(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        TextRange originalRange,
        TextRange userRange,
        out TextRange repairedOriginal,
        out TextRange repairedUser) =>
        TryRepairPreceding(
            request,
            source,
            originalRange,
            userRange,
            out repairedOriginal,
            out repairedUser)
        || TryRepairFollowing(
            request,
            source,
            originalRange,
            userRange,
            out repairedOriginal,
            out repairedUser);

    private static bool TryRepairPreceding(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        TextRange originalRange,
        TextRange userRange,
        out TextRange repairedOriginal,
        out TextRange repairedUser)
    {
        repairedOriginal = originalRange;
        repairedUser = userRange;

        if (!TextRangeNavigator.TryGetPreviousWord(
                request.OriginalText,
                source.OriginalTextRange,
                originalRange.InitialIndex,
                out var originalPrevious)
            || !TextRangeNavigator.TryGetPreviousWord(
                request.UserText,
                source.UserTextRange,
                userRange.InitialIndex,
                out var userPrevious))
        {
            return false;
        }

        if (TextRangeNavigator.TryGetPreviousWord(
                request.UserText,
                source.UserTextRange,
                userPrevious.InitialIndex,
                out var userAnchor)
            && TextRangeNavigator.AreMatchingWords(
                request.OriginalText,
                originalPrevious,
                request.UserText,
                userAnchor))
        {
            repairedUser = new TextRange(
                userPrevious.InitialIndex,
                userRange.FinalIndex);
            return true;
        }

        if (TextRangeNavigator.TryGetPreviousWord(
                request.OriginalText,
                source.OriginalTextRange,
                originalPrevious.InitialIndex,
                out var originalAnchor)
            && TextRangeNavigator.AreMatchingWords(
                request.OriginalText,
                originalAnchor,
                request.UserText,
                userPrevious))
        {
            repairedOriginal = new TextRange(
                originalPrevious.InitialIndex,
                originalRange.FinalIndex);
            return true;
        }

        return false;
    }

    private static bool TryRepairFollowing(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        TextRange originalRange,
        TextRange userRange,
        out TextRange repairedOriginal,
        out TextRange repairedUser)
    {
        repairedOriginal = originalRange;
        repairedUser = userRange;

        if (!TextRangeNavigator.TryGetNextWord(
                request.OriginalText,
                source.OriginalTextRange,
                originalRange.FinalIndex,
                out var originalNext)
            || !TextRangeNavigator.TryGetNextWord(
                request.UserText,
                source.UserTextRange,
                userRange.FinalIndex,
                out var userNext))
        {
            return false;
        }

        if (TextRangeNavigator.TryGetNextWord(
                request.UserText,
                source.UserTextRange,
                userNext.FinalIndex,
                out var userAnchor)
            && TextRangeNavigator.AreMatchingWords(
                request.OriginalText,
                originalNext,
                request.UserText,
                userAnchor))
        {
            repairedUser = new TextRange(
                userRange.InitialIndex,
                userNext.FinalIndex);
            return true;
        }

        if (TextRangeNavigator.TryGetNextWord(
                request.OriginalText,
                source.OriginalTextRange,
                originalNext.FinalIndex,
                out var originalAnchor)
            && TextRangeNavigator.AreMatchingWords(
                request.OriginalText,
                originalAnchor,
                request.UserText,
                userNext))
        {
            repairedOriginal = new TextRange(
                originalRange.InitialIndex,
                originalNext.FinalIndex);
            return true;
        }

        return false;
    }
}

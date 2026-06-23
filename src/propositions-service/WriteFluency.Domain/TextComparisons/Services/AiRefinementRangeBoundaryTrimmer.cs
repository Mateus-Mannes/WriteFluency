namespace WriteFluency.TextComparisons;

internal sealed class AiRefinementRangeBoundaryTrimmer
{
    public void Trim(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        ref TextRange originalRange,
        ref TextRange userRange)
    {
        TrimMatchingBoundaryWords(
            request.OriginalText,
            request.UserText,
            ref originalRange,
            ref userRange);
        TrimOneSidedMatchingBoundaryWords(
            request,
            source,
            ref originalRange,
            ref userRange);
    }

    private static void TrimMatchingBoundaryWords(
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

    private static void TrimOneSidedMatchingBoundaryWords(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        ref TextRange originalRange,
        ref TextRange userRange)
    {
        TrimTrailingCrossSideMatch(
            request.OriginalText,
            source.OriginalTextRange,
            ref originalRange,
            request.UserText,
            ref userRange);
        TrimTrailingCrossSideMatch(
            request.UserText,
            source.UserTextRange,
            ref userRange,
            request.OriginalText,
            ref originalRange);
        TrimLeadingCrossSideMatch(
            request.OriginalText,
            source.OriginalTextRange,
            ref originalRange,
            request.UserText,
            ref userRange);
        TrimLeadingCrossSideMatch(
            request.UserText,
            source.UserTextRange,
            ref userRange,
            request.OriginalText,
            ref originalRange);
    }

    private static void TrimTrailingCrossSideMatch(
        string outsideText,
        TextRange outsideSource,
        ref TextRange outsideRange,
        string includedText,
        ref TextRange includedRange)
    {
        if (!TextRangeNavigator.TryGetNextWord(
                outsideText,
                outsideSource,
                outsideRange.FinalIndex,
                out var outsideNext)
            || !TextRangeNavigator.TryGetLastWord(
                includedText,
                includedRange,
                out var includedLast)
            || !TextRangeNavigator.AreMatchingWords(
                outsideText,
                outsideNext,
                includedText,
                includedLast)
            || !CanRemoveWord(includedRange, includedLast))
        {
            return;
        }

        includedRange = TextRangeNavigator.TrimTrailingWord(
            includedText,
            includedRange,
            includedLast);
    }

    private static void TrimLeadingCrossSideMatch(
        string outsideText,
        TextRange outsideSource,
        ref TextRange outsideRange,
        string includedText,
        ref TextRange includedRange)
    {
        if (!TextRangeNavigator.TryGetPreviousWord(
                outsideText,
                outsideSource,
                outsideRange.InitialIndex,
                out var outsidePrevious)
            || !TextRangeNavigator.TryGetFirstWord(
                includedText,
                includedRange,
                out var includedFirst)
            || !TextRangeNavigator.AreMatchingWords(
                outsideText,
                outsidePrevious,
                includedText,
                includedFirst)
            || !CanRemoveWord(includedRange, includedFirst))
        {
            return;
        }

        includedRange = TextRangeNavigator.TrimLeadingWord(
            includedText,
            includedRange,
            includedFirst);
    }

    private static bool CanRemoveWord(TextRange range, TextRange word) =>
        word.InitialIndex > range.InitialIndex
        || word.FinalIndex < range.FinalIndex;
}

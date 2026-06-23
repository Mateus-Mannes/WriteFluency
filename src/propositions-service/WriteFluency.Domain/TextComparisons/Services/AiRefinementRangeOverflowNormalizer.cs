namespace WriteFluency.TextComparisons;

internal sealed class AiRefinementRangeOverflowNormalizer
{
    public bool TryNormalize(
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

        if (!CanClampLeadingOverflow(candidate, source, fullText)
            || !CanClampTrailingOverflow(candidate, source, fullText))
        {
            return false;
        }

        normalized = new TextRange(
            Math.Max(candidate.InitialIndex, source.InitialIndex),
            Math.Min(candidate.FinalIndex, source.FinalIndex));
        return normalized.FinalIndex >= normalized.InitialIndex
            && IsContainedBy(normalized, source);
    }

    private static bool CanClampLeadingOverflow(
        TextRange candidate,
        TextRange source,
        string fullText)
    {
        if (candidate.InitialIndex >= source.InitialIndex)
        {
            return true;
        }

        if (TextRangeNavigator.ContainsOnlyIgnorableCharacters(
                fullText,
                candidate.InitialIndex,
                Math.Min(candidate.FinalIndex, source.InitialIndex - 1)))
        {
            return true;
        }

        if (source.InitialIndex == 0
            || !TextRangeNavigator.IsIgnorableCharacter(
                fullText[source.InitialIndex - 1]))
        {
            return false;
        }

        var previousWord = FindPreviousWord(fullText, source.InitialIndex);
        if (previousWord is null
            || candidate.InitialIndex < previousWord.InitialIndex)
        {
            return false;
        }

        return candidate.InitialIndex > previousWord.InitialIndex
            || AiRefinementAlignmentPolicy.IsFunctionWord(
                TextRangeNavigator.Slice(fullText, previousWord));
    }

    private static bool CanClampTrailingOverflow(
        TextRange candidate,
        TextRange source,
        string fullText)
    {
        if (candidate.FinalIndex <= source.FinalIndex)
        {
            return true;
        }

        if (TextRangeNavigator.ContainsOnlyIgnorableCharacters(
                fullText,
                Math.Max(candidate.InitialIndex, source.FinalIndex + 1),
                candidate.FinalIndex))
        {
            return true;
        }

        if (source.FinalIndex >= fullText.Length - 1
            || !TextRangeNavigator.IsIgnorableCharacter(
                fullText[source.FinalIndex + 1]))
        {
            return false;
        }

        var nextWord = FindNextWord(fullText, source.FinalIndex);
        if (nextWord is null
            || candidate.FinalIndex > nextWord.FinalIndex)
        {
            return false;
        }

        return candidate.FinalIndex < nextWord.FinalIndex
            || AiRefinementAlignmentPolicy.IsFunctionWord(
                TextRangeNavigator.Slice(fullText, nextWord));
    }

    private static TextRange? FindPreviousWord(string text, int beforeIndex)
    {
        var end = beforeIndex - 1;
        while (end >= 0 && !TextRangeNavigator.IsWordCharacter(text[end]))
        {
            end--;
        }

        if (end < 0)
        {
            return null;
        }

        var start = end;
        while (start > 0
               && TextRangeNavigator.IsWordCharacter(text[start - 1]))
        {
            start--;
        }

        return new TextRange(start, end);
    }

    private static TextRange? FindNextWord(string text, int afterIndex)
    {
        var start = afterIndex + 1;
        while (start < text.Length
               && !TextRangeNavigator.IsWordCharacter(text[start]))
        {
            start++;
        }

        if (start >= text.Length)
        {
            return null;
        }

        var end = start;
        while (end < text.Length - 1
               && TextRangeNavigator.IsWordCharacter(text[end + 1]))
        {
            end++;
        }

        return new TextRange(start, end);
    }

    private static bool IsContainedBy(TextRange candidate, TextRange source) =>
        candidate.InitialIndex >= source.InitialIndex
        && candidate.FinalIndex <= source.FinalIndex;
}

namespace WriteFluency.TextComparisons;

internal static class TextRangeNavigator
{
    public static bool IsValid(TextRange range, int textLength) =>
        textLength > 0
        && range.InitialIndex >= 0
        && range.FinalIndex >= range.InitialIndex
        && range.FinalIndex < textLength;

    public static bool TryNormalizeToSource(
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
            && !ContainsOnlyIgnorableCharacters(
                fullText,
                candidate.InitialIndex,
                Math.Min(candidate.FinalIndex, source.InitialIndex - 1)))
        {
            return false;
        }

        if (candidate.FinalIndex > source.FinalIndex
            && !ContainsOnlyIgnorableCharacters(
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

    public static bool TryNormalizeBoundaries(
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

    public static bool ContainsOnlyIgnorableCharacters(
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
            if (!IsIgnorableCharacter(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TrySlice(
        string text,
        TextRange range,
        out string snippet)
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

    public static bool HasCompleteWordBoundaries(
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

    public static bool TryGetFirstWord(
        string text,
        TextRange range,
        out TextRange word)
    {
        var start = range.InitialIndex;
        while (start <= range.FinalIndex && !IsWordCharacter(text[start]))
        {
            start++;
        }

        if (start > range.FinalIndex)
        {
            word = new TextRange(0, 0);
            return false;
        }

        var end = start;
        while (end < range.FinalIndex && IsWordCharacter(text[end + 1]))
        {
            end++;
        }

        word = new TextRange(start, end);
        return true;
    }

    public static bool TryGetLastWord(
        string text,
        TextRange range,
        out TextRange word)
    {
        var end = range.FinalIndex;
        while (end >= range.InitialIndex && !IsWordCharacter(text[end]))
        {
            end--;
        }

        if (end < range.InitialIndex)
        {
            word = new TextRange(0, 0);
            return false;
        }

        var start = end;
        while (start > range.InitialIndex && IsWordCharacter(text[start - 1]))
        {
            start--;
        }

        word = new TextRange(start, end);
        return true;
    }

    public static bool TryGetPreviousWord(
        string text,
        TextRange source,
        int beforeIndex,
        out TextRange word)
    {
        var end = beforeIndex - 1;
        while (end >= source.InitialIndex && !IsWordCharacter(text[end]))
        {
            end--;
        }

        if (end < source.InitialIndex)
        {
            word = new TextRange(0, 0);
            return false;
        }

        var start = end;
        while (start > source.InitialIndex && IsWordCharacter(text[start - 1]))
        {
            start--;
        }

        word = new TextRange(start, end);
        return true;
    }

    public static bool TryGetNextWord(
        string text,
        TextRange source,
        int afterIndex,
        out TextRange word)
    {
        var start = afterIndex + 1;
        while (start <= source.FinalIndex && !IsWordCharacter(text[start]))
        {
            start++;
        }

        if (start > source.FinalIndex)
        {
            word = new TextRange(0, 0);
            return false;
        }

        var end = start;
        while (end < source.FinalIndex && IsWordCharacter(text[end + 1]))
        {
            end++;
        }

        word = new TextRange(start, end);
        return true;
    }

    public static bool AreMatchingWords(
        string originalText,
        TextRange originalWord,
        string userText,
        TextRange userWord) =>
        string.Equals(
            Slice(originalText, originalWord),
            Slice(userText, userWord),
            StringComparison.OrdinalIgnoreCase);

    public static TextRange TrimLeadingWord(
        string text,
        TextRange range,
        TextRange word)
    {
        var start = word.FinalIndex + 1;
        while (start <= range.FinalIndex && IsIgnorableCharacter(text[start]))
        {
            start++;
        }

        return new TextRange(start, range.FinalIndex);
    }

    public static TextRange TrimTrailingWord(
        string text,
        TextRange range,
        TextRange word)
    {
        var end = word.InitialIndex - 1;
        while (end >= range.InitialIndex && IsIgnorableCharacter(text[end]))
        {
            end--;
        }

        return new TextRange(range.InitialIndex, end);
    }

    private static bool IsContainedBy(TextRange candidate, TextRange source) =>
        candidate.InitialIndex >= source.InitialIndex
        && candidate.FinalIndex <= source.FinalIndex;

    private static bool IsWordCharacter(char character) =>
        char.IsLetterOrDigit(character);

    private static bool IsIgnorableCharacter(char character) =>
        char.IsWhiteSpace(character)
        || char.IsPunctuation(character);

    private static bool IsIgnorableBoundaryCharacter(char character) =>
        IsIgnorableCharacter(character)
        && character is not '\'' and not '\u2018' and not '\u2019';

    private static string Slice(string text, TextRange range) =>
        text.Substring(
            range.InitialIndex,
            range.FinalIndex - range.InitialIndex + 1);
}

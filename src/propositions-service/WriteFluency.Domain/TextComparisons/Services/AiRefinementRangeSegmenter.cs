namespace WriteFluency.TextComparisons;

internal sealed class AiRefinementRangeSegmenter
{
    public IReadOnlyList<(TextRange Original, TextRange User)> Split(
        string originalText,
        string userText,
        TextRange originalRange,
        TextRange userRange,
        bool preserveExistingSplit = false)
    {
        var segments = new List<(TextRange Original, TextRange User)>();
        SplitRecursive(
            originalText,
            userText,
            originalRange,
            userRange,
            preserveExistingSplit,
            segments);
        return segments;
    }

    private static void SplitRecursive(
        string originalText,
        string userText,
        TextRange originalRange,
        TextRange userRange,
        bool preserveExistingSplit,
        List<(TextRange Original, TextRange User)> segments)
    {
        var originalWords = TextRangeNavigator.GetWords(
            originalText,
            originalRange);
        var userWords = TextRangeNavigator.GetWords(userText, userRange);
        var anchor = FindReliableAnchor(
            originalText,
            userText,
            originalWords,
            userWords,
            preserveExistingSplit);

        if (anchor is null
            || !TryCreateSides(
                originalWords,
                userWords,
                anchor.Value,
                out var left,
                out var right)
            || AreEqual(originalText, userText, left)
            || AreEqual(originalText, userText, right))
        {
            segments.Add((originalRange, userRange));
            return;
        }

        SplitRecursive(
            originalText,
            userText,
            left.Original,
            left.User,
            preserveExistingSplit,
            segments);
        SplitRecursive(
            originalText,
            userText,
            right.Original,
            right.User,
            preserveExistingSplit,
            segments);
    }

    private static Anchor? FindReliableAnchor(
        string originalText,
        string userText,
        IReadOnlyList<TextRange> originalWords,
        IReadOnlyList<TextRange> userWords,
        bool preserveExistingSplit)
    {
        var candidates = new List<Anchor>();
        for (var originalIndex = 1;
             originalIndex < originalWords.Count - 1;
             originalIndex++)
        {
            for (var userIndex = 1;
                 userIndex < userWords.Count - 1;
                 userIndex++)
            {
                if (!WordsMatch(
                        originalText,
                        originalWords[originalIndex],
                        userText,
                        userWords[userIndex])
                    || IsContinuationOfPreviousMatch(
                        originalText,
                        userText,
                        originalWords,
                        userWords,
                        originalIndex,
                        userIndex))
                {
                    continue;
                }

                var length = 1;
                while (originalIndex + length < originalWords.Count - 1
                       && userIndex + length < userWords.Count - 1
                       && WordsMatch(
                           originalText,
                           originalWords[originalIndex + length],
                           userText,
                           userWords[userIndex + length]))
                {
                    length++;
                }

                var anchor = new Anchor(
                    originalIndex,
                    originalIndex + length - 1,
                    userIndex,
                    userIndex + length - 1,
                    Enumerable.Range(0, length)
                        .Select(offset => TextRangeNavigator.Slice(
                            originalText,
                            originalWords[originalIndex + offset]))
                        .ToList());
                if (AiRefinementAlignmentPolicy.IsReliableAnchor(
                        anchor.Words,
                        originalWords.Count,
                        userWords.Count,
                        anchor.OriginalStartIndex,
                        anchor.OriginalEndIndex,
                        anchor.UserStartIndex,
                        anchor.UserEndIndex,
                        preserveExistingSplit))
                {
                    candidates.Add(anchor);
                }
            }
        }

        var longestLength = candidates
            .Select(candidate => candidate.Length)
            .DefaultIfEmpty()
            .Max();
        var longest = candidates
            .Where(candidate => candidate.Length == longestLength)
            .ToList();
        return longest.Count == 1
            ? longest[0]
            : null;
    }

    private static bool TryCreateSides(
        IReadOnlyList<TextRange> originalWords,
        IReadOnlyList<TextRange> userWords,
        Anchor anchor,
        out (TextRange Original, TextRange User) left,
        out (TextRange Original, TextRange User) right)
    {
        left = default;
        right = default;
        if (anchor.OriginalStartIndex == 0
            || anchor.UserStartIndex == 0
            || anchor.OriginalEndIndex >= originalWords.Count - 1
            || anchor.UserEndIndex >= userWords.Count - 1)
        {
            return false;
        }

        left = (
            new TextRange(
                originalWords[0].InitialIndex,
                originalWords[anchor.OriginalStartIndex - 1].FinalIndex),
            new TextRange(
                userWords[0].InitialIndex,
                userWords[anchor.UserStartIndex - 1].FinalIndex));
        right = (
            new TextRange(
                originalWords[anchor.OriginalEndIndex + 1].InitialIndex,
                originalWords[^1].FinalIndex),
            new TextRange(
                userWords[anchor.UserEndIndex + 1].InitialIndex,
                userWords[^1].FinalIndex));
        return true;
    }

    private static bool IsContinuationOfPreviousMatch(
        string originalText,
        string userText,
        IReadOnlyList<TextRange> originalWords,
        IReadOnlyList<TextRange> userWords,
        int originalIndex,
        int userIndex) =>
        originalIndex > 0
        && userIndex > 0
        && WordsMatch(
            originalText,
            originalWords[originalIndex - 1],
            userText,
            userWords[userIndex - 1]);

    private static bool WordsMatch(
        string originalText,
        TextRange originalWord,
        string userText,
        TextRange userWord) =>
        string.Equals(
            TextRangeNavigator.Slice(originalText, originalWord),
            TextRangeNavigator.Slice(userText, userWord),
            StringComparison.OrdinalIgnoreCase);

    private static bool AreEqual(
        string originalText,
        string userText,
        (TextRange Original, TextRange User) ranges) =>
        string.Equals(
            TextRangeNavigator.Slice(originalText, ranges.Original),
            TextRangeNavigator.Slice(userText, ranges.User),
            StringComparison.OrdinalIgnoreCase);

    private readonly record struct Anchor(
        int OriginalStartIndex,
        int OriginalEndIndex,
        int UserStartIndex,
        int UserEndIndex,
        IReadOnlyList<string> Words)
    {
        public int Length => OriginalEndIndex - OriginalStartIndex + 1;
    }
}

namespace WriteFluency.TextComparisons;

internal static class AiRefinementAlignmentPolicy
{
    private static readonly HashSet<string> FunctionWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "at", "but", "for", "from", "in", "of",
            "on", "or", "the", "to", "with"
        };

    public static bool IsFunctionWord(string word) =>
        FunctionWords.Contains(word);

    public static bool IsReliableAnchor(
        IReadOnlyList<string> words,
        int originalWordCount,
        int userWordCount,
        int originalStartIndex,
        int originalEndIndex,
        int userStartIndex,
        int userEndIndex,
        bool preserveExistingSplit)
    {
        if (words.Any(word => !IsFunctionWord(word)))
        {
            return true;
        }

        if (words.Count > 1)
        {
            return preserveExistingSplit;
        }

        if (originalWordCount > 7 || userWordCount > 7)
        {
            return false;
        }

        var prefixDrift = userStartIndex - originalStartIndex;
        var originalSuffixCount =
            originalWordCount - originalEndIndex - 1;
        var userSuffixCount =
            userWordCount - userEndIndex - 1;

        return prefixDrift == userSuffixCount - originalSuffixCount;
    }
}

namespace WriteFluency.TextComparisons;

public class TokenComparisonService
{
    public void AddTokenComparison(
        ref int tokenAlignmentIndex,
        List<AlignedTokens> alignedTokens,
        List<TextComparison> textComparisons,
        string originalText,
        string userText)
    {
        var token = alignedTokens[tokenAlignmentIndex];
        if (token.OriginalToken == null || token.UserToken == null)
        {
            AddComparisonWithMissingToken(
                ref tokenAlignmentIndex,
                alignedTokens,
                textComparisons,
                originalText,
                userText
                );
        }
        else if (token.OriginalToken.Token != token.UserToken.Token)
        {
            AddComparison(
                token.OriginalToken!.TextRange,
                token.UserToken!.TextRange,
                userText,
                textComparisons);
        }
    }

    private void AddComparisonWithMissingToken(
        ref int tokenAlignmentIndex,
        List<AlignedTokens> alignedTokens,
        List<TextComparison> textComparisons,
        string originalText,
        string userText)
    {
        var previous = GetFollowingFullAlignment(DirectionEnum.Previous, tokenAlignmentIndex, alignedTokens);
        var next = GetFollowingFullAlignment(DirectionEnum.Next, tokenAlignmentIndex, alignedTokens);
        if (next != null) tokenAlignmentIndex++;
        AddComparison(
            new TextRange(previous?.OriginalToken, next?.OriginalToken, originalText),
            new TextRange(previous?.UserToken, next?.UserToken, userText),
            userText,
            textComparisons
        );
    }

    /// <summary>
    /// Gets the previous or next alignment that has both tokens identified.
    /// </summary>
    private AlignedTokens? GetFollowingFullAlignment(
        DirectionEnum direction,
        int tokenAlignmentIndex,
        List<AlignedTokens> alignedTokens)
    {
        TextToken? userToken = null;
        TextToken? originalToken = null;

        int step = direction == DirectionEnum.Previous ? -1 : 1;
        int index = tokenAlignmentIndex + step;

        while (index >= 0 && index < alignedTokens.Count)
        {
            var alignment = alignedTokens[index];
            userToken ??= alignment.UserToken;
            originalToken ??= alignment.OriginalToken;
            if (userToken != null && originalToken != null) break;
            index += step;
        }

        return new AlignedTokens(originalToken, userToken);
    }

    private void AddComparison(
        TextRange originalTextRange,
        TextRange userTextRange,
        string userText,
        List<TextComparison> textComparisons)
    {
        var lastComparison = textComparisons.LastOrDefault();
        if (lastComparison != null && IsSequentialComparison(userText, lastComparison, userTextRange))
        {
            lastComparison.IncrementComparison(originalTextRange, userTextRange);
        }
        else
        {
            textComparisons.Add(new TextComparison(originalTextRange, userTextRange));
        }
    }

    private bool IsSequentialComparison(
        string userText,
        TextComparison lastTextComparison,
        TextRange userTextRange)
    {
        if (lastTextComparison.UserTextRange.FinalIndex >= userTextRange.InitialIndex) return true;
        int index = userTextRange.InitialIndex - 1;
        while (index >= 0 && (char.IsWhiteSpace(userText[index]) || char.IsPunctuation(userText[index])))
            index--;
        return index == lastTextComparison.UserTextRange.FinalIndex;
    }
}

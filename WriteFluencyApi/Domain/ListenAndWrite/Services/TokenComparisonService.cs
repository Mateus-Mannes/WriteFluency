namespace WriteFluencyApi.ListenAndWrite.Domain;

public class TokenComparisonService : ITokenComparisonService
{
    public void AddTokenComparison(
        ref int tokenAlignmentIndex,
        List<AlignedTokensDto> alignedTokens,
        List<TextComparisonDto> textComparisons,
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
        List<AlignedTokensDto> alignedTokens,
        List<TextComparisonDto> textComparisons,
        string originalText,
        string userText)
    {
        var previous = GetFollowingFullAlignment(DirectionEnum.Previous, tokenAlignmentIndex, alignedTokens);
        var next = GetFollowingFullAlignment(DirectionEnum.Next, tokenAlignmentIndex, alignedTokens);
        if (next != null) tokenAlignmentIndex++;
        AddComparison(
            new TextRangeDto(previous?.OriginalToken, next?.OriginalToken, originalText),
            new TextRangeDto(previous?.UserToken, next?.UserToken, userText),
            userText,
            textComparisons
        );
    }

    /// <summary>
    /// Gets the previous or next alignment that has both tokens identified.
    /// </summary>
    private AlignedTokensDto? GetFollowingFullAlignment(
        DirectionEnum direction,
        int tokenAlignmentIndex,
        List<AlignedTokensDto> alignedTokens)
    {
        TextTokenDto? userToken = null;
        TextTokenDto? originalToken = null;

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

        return new AlignedTokensDto(originalToken, userToken);
    }

    private void AddComparison(
        TextRangeDto originalTextRange,
        TextRangeDto userTextRange,
        string userText,
        List<TextComparisonDto> textComparisons)
    {
        var lastComparison = textComparisons.LastOrDefault();
        if (lastComparison != null && IsSequentialComparison(userText, lastComparison, userTextRange))
        {
            lastComparison.IncrementComparison(originalTextRange, userTextRange);
        }
        else
        {
            textComparisons.Add(new TextComparisonDto(originalTextRange, userTextRange));
        }
    }

    private bool IsSequentialComparison(
        string userText,
        TextComparisonDto lastTextComparison,
        TextRangeDto userTextRange)
    {
        if (lastTextComparison.UserTextRange.FinalIndex >= userTextRange.InitialIndex) return true;
        int index = userTextRange.InitialIndex - 1;
        while (index >= 0 && (char.IsWhiteSpace(userText[index]) || char.IsPunctuation(userText[index])))
            index--;
        return index == lastTextComparison.UserTextRange.FinalIndex;
    }
}

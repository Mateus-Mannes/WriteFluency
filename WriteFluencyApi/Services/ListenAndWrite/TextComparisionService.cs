public class TextComparisionService {

    private const double SimilartyThresholdPercentage = 0.60; 
    private readonly LevenshteinDistanceService _levenshteinDistanceService;
    private readonly NeedlemanWunschAlignmentService _needlemanWunschAlignmentService;
    private readonly TokenizeTextService _tokenizeTextService;

    public TextComparisionService(LevenshteinDistanceService levenshteinDistanceService, 
        NeedlemanWunschAlignmentService needlemanWunschAlignmentService, 
        TokenizeTextService tokenizeTextService)
    {
        _levenshteinDistanceService = levenshteinDistanceService;
        _needlemanWunschAlignmentService = needlemanWunschAlignmentService;
        _tokenizeTextService = tokenizeTextService;
    }

    public List<TextComparisionDto> CompareTexts(string originalText, string userText) {

        if(!IsMinimalSimilar(originalText, userText)) 
            return new List<TextComparisionDto>() { 
                new TextComparisionDto(
                    new TextRangeDto(0, originalText.Length - 1), 
                    new TextRangeDto(0, userText.Length - 1)) 
                };
        
        var originalTokens = _tokenizeTextService.TokenizeText(originalText);
        var userTokens = _tokenizeTextService.TokenizeText(userText);

        (int[,] scoreMatrix, int[,] tracebackMatrix) = 
            _needlemanWunschAlignmentService.NeedlemanWunschAlignment(
                originalTokens.Select(x => x.Token).ToList(), 
                userTokens.Select(x => x.Token).ToList());

        var alignedTokens = _needlemanWunschAlignmentService.GetAlignedTokens(originalTokens, userTokens, tracebackMatrix);

        List<TextComparisionDto> textComparisions = new List<TextComparisionDto>();

        for(int i = 0; i < alignedTokens.Count; i++) 
            CompareTokens(ref i, alignedTokens, textComparisions, originalText, userText);

        GetSubStrings(textComparisions, originalText, userText);

        return textComparisions;
    }

    private bool IsMinimalSimilar(string originalText, string userText) {
        int distance = _levenshteinDistanceService.ComputeDistance(originalText, userText);
        double similarity =  1 - (double)distance / Math.Max(originalText.Length, userText.Length);
        return similarity >= SimilartyThresholdPercentage;
    }

    private void CompareTokens(
        ref int tokenAlignmentIndex,
        List<AlignedTokensDto> alignedTokens, 
        List<TextComparisionDto> textComparisions,
        string originalText,
        string userText)
    {
        var token = alignedTokens[tokenAlignmentIndex];
        if(token.OriginalToken == null || token.UserToken == null)
        {
            var privious = alignedTokens.ElementAtOrDefault(tokenAlignmentIndex - 1);
            var next = alignedTokens.ElementAtOrDefault(tokenAlignmentIndex + 1);
            if(next != null) tokenAlignmentIndex++;
            AddComparision(
                new TextRangeDto(privious?.OriginalToken?.TextRange.InitialIndex ?? 0, 
                    next?.OriginalToken?.TextRange.FinalIndex ?? originalText.Length - 1),
                new TextRangeDto(privious?.UserToken?.TextRange.InitialIndex ?? 0, 
                    next?.UserToken?.TextRange.FinalIndex ?? userText.Length - 1),
                userText,
                textComparisions
            );
        }
        else if(token.OriginalToken.Token != token.UserToken.Token)
            AddComparision(
                token.OriginalToken!.TextRange, 
                token.UserToken!.TextRange,
                userText, 
                textComparisions);
    }

    private void AddComparision(
        TextRangeDto originalTextRange,
        TextRangeDto userTextRange,
        string userText,
        List<TextComparisionDto> textComparisions)
    {
        var lastComparision = textComparisions.LastOrDefault();
        if(lastComparision != null 
            && (IsSequential(userText, lastComparision, userTextRange)
                || lastComparision.UserTextRange.FinalIndex >= userTextRange.InitialIndex)) 
        {
            lastComparision.UserTextRange = 
                new TextRangeDto(lastComparision.UserTextRange.InitialIndex, userTextRange.FinalIndex);
            lastComparision.OriginalTextRange =
                new TextRangeDto(lastComparision.OriginalTextRange.InitialIndex, originalTextRange.FinalIndex);
        }
        else
            textComparisions.Add(new TextComparisionDto(originalTextRange, userTextRange));
    }

    private bool IsSequential(string userText, TextComparisionDto lastTextComparision, TextRangeDto userTextRange)
    {
        int index = userTextRange.InitialIndex - 1;
        while(index >= 0 && (char.IsWhiteSpace(userText[index]) || char.IsPunctuation(userText[index])))
            index--;
        return index == lastTextComparision.UserTextRange.FinalIndex;
    }

    private void GetSubStrings(List<TextComparisionDto> textComparisions, string originalText, string userText)
    {
        foreach(var comparision in textComparisions)
        {
            comparision.OriginalText =  originalText.Substring(comparision.OriginalTextRange.InitialIndex, 
                comparision.OriginalTextRange.FinalIndex - comparision.OriginalTextRange.InitialIndex + 1);
            comparision.UserText = userText.Substring(comparision.UserTextRange.InitialIndex, 
                comparision.UserTextRange.FinalIndex - comparision.UserTextRange.InitialIndex + 1);
        }
    }

}
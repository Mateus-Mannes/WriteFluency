public class TextComparisionService {

    private const double SimilartyThresholdPercentage = 0.70; 
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
                    new TextRangeDto(0, userText.Length - 1), 
                    new TextRangeDto(0, originalText.Length - 1),
                    0) 
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
                textComparisions,
                tokenAlignmentIndex
            );
        }
        else if(token.OriginalToken.Token != token.UserToken.Token)
            AddComparision(
                token.OriginalToken!.TextRange, 
                token.UserToken!.TextRange, 
                textComparisions, 
                tokenAlignmentIndex);
    }

    private void AddComparision(
        TextRangeDto originalTextRange,
        TextRangeDto userTextRange,
        List<TextComparisionDto> textComparisions,
        int tokenAlignmentIndex)
    {
        var lastComparision = textComparisions.LastOrDefault();
        if(lastComparision != null 
            && (lastComparision.TokenAlignmentIndex == tokenAlignmentIndex - 1
                || lastComparision.UserTextRange.InitialIndex == userTextRange.InitialIndex)) 
        {
            lastComparision.UserTextRange = 
                new TextRangeDto(lastComparision.UserTextRange.InitialIndex, userTextRange.FinalIndex);
            lastComparision.TokenAlignmentIndex = tokenAlignmentIndex;
        }
        else
            textComparisions.Insert(0, new TextComparisionDto(originalTextRange, userTextRange, tokenAlignmentIndex));
    }

}
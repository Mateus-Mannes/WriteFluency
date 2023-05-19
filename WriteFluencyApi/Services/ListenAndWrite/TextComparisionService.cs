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
                    (0, userText.Length - 1), 
                    (0, originalText.Length - 1)) 
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
            CompareTokens(i, alignedTokens, textComparisions, originalText, userText);

        return textComparisions;
    }

    private bool IsMinimalSimilar(string originalText, string userText) {
        int distance = _levenshteinDistanceService.ComputeDistance(originalText, userText);
        double similarity =  1 - (double)distance / Math.Max(originalText.Length, userText.Length);
        return similarity >= SimilartyThresholdPercentage;
    }

    private void CompareTokens(
        int tokensIndex,
        List<(TextTokenDto?, TextTokenDto?)> alignedTokens, 
        List<TextComparisionDto> textComparisions,
        string originalText,
        string userText)
    {
        var token = alignedTokens[tokensIndex];
        if(token.Item1 == null || token.Item2 == null)
        {
            var priviousToken = alignedTokens.ElementAtOrDefault(tokensIndex - 1);
            var nextToken = alignedTokens.ElementAtOrDefault(tokensIndex + 1);
            AddComparision(
                (priviousToken.Item1?.TextRangeIndex.Item2 ?? 0, 
                    nextToken.Item1?.TextRangeIndex.Item1 ?? originalText.Length - 1),
                (priviousToken.Item2?.TextRangeIndex.Item2 ?? 0, 
                    nextToken.Item2?.TextRangeIndex.Item1 ?? userText.Length - 1),
                textComparisions
            );
        }
        else if(token.Item1 != token.Item2)
            AddComparision(token.Item1!.TextRangeIndex, token.Item2!.TextRangeIndex, textComparisions);
    }

    private void AddComparision(
        (int, int) originalTextRangeIndex,
        (int, int) userTextRangeIndex,
        List<TextComparisionDto> textComparisions)
    {
        var lastComparision = textComparisions.LastOrDefault();
        if(lastComparision != null && lastComparision.UserTextHilightedArea.Item2 
            == userTextRangeIndex.Item2) 
        {
            lastComparision.UserTextHilightedArea = 
                (lastComparision.UserTextHilightedArea.Item1, userTextRangeIndex.Item2);
        }
        else
        {
            textComparisions.Add(new TextComparisionDto(
                (originalTextRangeIndex.Item1, originalTextRangeIndex.Item2),
                (userTextRangeIndex.Item1, userTextRangeIndex.Item2)));
        }
    }

}
namespace WriteFluencyApi.ListenAndWrite.Domain;

public class TextComparisonService {

    private const double SimilartyThresholdPercentage = 0.60; 
    private readonly LevenshteinDistanceService _levenshteinDistanceService;
    private readonly TextAlignementService _textAlignementService;
    private readonly TokenComparisonService _tokeComparisonService;

    public TextComparisonService(
        LevenshteinDistanceService levenshteinDistanceService, 
        TextAlignementService textAlignementService,
        TokenComparisonService tokeComparisonService)
    {
        _levenshteinDistanceService = levenshteinDistanceService;
        _textAlignementService = textAlignementService;
        _tokeComparisonService = tokeComparisonService;
    }

    public List<TextComparisonDto> CompareTexts(string originalText, string userText) {

        if(!IsMinimalSimilar(originalText, userText)) 
            return new() { new TextComparisonDto(originalText, userText) };

        var alignedTokens = _textAlignementService.AlignTexts(originalText, userText);

        List<TextComparisonDto> textComparisons = new List<TextComparisonDto>();

        for(int i = 0; i < alignedTokens.Count; i++)
        {
            _tokeComparisonService.AddTokenComparison(
                ref i,
                alignedTokens,
                textComparisons,
                originalText,
                userText);
        }

        AddSubStrings(textComparisons, originalText, userText);

        return textComparisons;
    }

    private bool IsMinimalSimilar(string originalText, string userText) {
        int distance = _levenshteinDistanceService.ComputeDistance(originalText, userText);
        double similarity =  1 - (double)distance / Math.Max(originalText.Length, userText.Length);
        return similarity >= SimilartyThresholdPercentage;
    }

    private void AddSubStrings(List<TextComparisonDto> textComparisons, string originalText, string userText)
    {
        foreach(var Comparison in textComparisons)
        {
            Comparison.OriginalText =  originalText.Substring(Comparison.OriginalTextRange.InitialIndex, 
                Comparison.OriginalTextRange.FinalIndex - Comparison.OriginalTextRange.InitialIndex + 1);
            Comparison.UserText = userText.Substring(Comparison.UserTextRange.InitialIndex, 
                Comparison.UserTextRange.FinalIndex - Comparison.UserTextRange.InitialIndex + 1);
        }
    }
}
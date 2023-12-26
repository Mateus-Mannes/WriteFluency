namespace WriteFluencyApi.ListenAndWrite.Domain;

public class TextComparisonService : ITextComparisonService
{

    private const double SimilartyThresholdPercentage = 0.60; 
    private readonly ILevenshteinDistanceService _levenshteinDistanceService;
    private readonly ITextAlignmentService _textAlignmentService;
    private readonly ITokenComparisonService _tokeComparisonService;

    public TextComparisonService(
        ILevenshteinDistanceService levenshteinDistanceService, 
        ITextAlignmentService textAlignmentService,
        ITokenComparisonService tokeComparisonService)
    {
        _levenshteinDistanceService = levenshteinDistanceService;
        _textAlignmentService = textAlignmentService;
        _tokeComparisonService = tokeComparisonService;
    }

    public List<TextComparisonDto> CompareTexts(string originalText, string userText) {

        if(!IsMinimalSimilar(originalText, userText)) 
            return new() { new TextComparisonDto(originalText, userText) };

        var alignedTokens = _textAlignmentService.AlignTexts(originalText, userText);

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
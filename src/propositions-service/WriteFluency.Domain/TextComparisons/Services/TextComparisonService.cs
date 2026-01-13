namespace WriteFluency.TextComparisons;

public class TextComparisonService
{

    private const double SimilartyThresholdPercentage = 0.05;
    private readonly LevenshteinDistanceService _levenshteinDistanceService;
    private readonly TextAlignmentService _textAlignmentService;
    private readonly TokenComparisonService _tokeComparisonService;

    public TextComparisonService(
        LevenshteinDistanceService levenshteinDistanceService,
        TextAlignmentService textAlignmentService,
        TokenComparisonService tokeComparisonService)
    {
        _levenshteinDistanceService = levenshteinDistanceService;
        _textAlignmentService = textAlignmentService;
        _tokeComparisonService = tokeComparisonService;
    }

    public TextComparisonResult CompareTexts(string originalText, string userText)
    {
        if (!IsMinimalSimilar(originalText, userText))
            return new TextComparisonResult(originalText, userText, 0, [ new TextComparison(originalText, userText) ]);

        var alignedTokens = _textAlignmentService.AlignTexts(originalText, userText);

        List<TextComparison> textComparisons = new List<TextComparison>();

        for (int i = 0; i < alignedTokens.Count; i++)
        {
            _tokeComparisonService.AddTokenComparison(
                ref i,
                alignedTokens,
                textComparisons,
                originalText,
                userText);
        }

        AddSubStrings(textComparisons, originalText, userText);

        var accuracy = CalculateAccuracy(originalText, textComparisons);

        return new TextComparisonResult(originalText, userText, accuracy, textComparisons);
    }

    private bool IsMinimalSimilar(string originalText, string userText)
    {
        int distance = _levenshteinDistanceService.ComputeDistance(originalText, userText);
        double similarity = 1 - (double)distance / Math.Max(originalText.Length, userText.Length);
        return similarity >= SimilartyThresholdPercentage;
    }

    private void AddSubStrings(List<TextComparison> textComparisons, string originalText, string userText)
    {
        foreach (var Comparison in textComparisons)
        {
            Comparison.OriginalText = originalText.Substring(Comparison.OriginalTextRange.InitialIndex,
                Comparison.OriginalTextRange.FinalIndex - Comparison.OriginalTextRange.InitialIndex + 1);
            Comparison.UserText = userText.Substring(Comparison.UserTextRange.InitialIndex,
                Comparison.UserTextRange.FinalIndex - Comparison.UserTextRange.InitialIndex + 1);
        }
    }

    private double CalculateAccuracy(string originalText, List<TextComparison> comparisons)
    {
        if (originalText == null || originalText.Length == 0)
            return 0;   

        var comparisonsLength = comparisons.Sum(c => c.OriginalText?.Length ?? 0);
        return 1 - ((double)comparisonsLength / originalText.Length);
    }
}
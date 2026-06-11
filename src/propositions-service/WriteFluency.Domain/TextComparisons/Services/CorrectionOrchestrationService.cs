namespace WriteFluency.TextComparisons;

public sealed class CorrectionOrchestrationService
{
    private readonly TextComparisonService _textComparisonService;
    private readonly DeterministicTextEquivalenceService _equivalenceService;

    public CorrectionOrchestrationService(
        TextComparisonService textComparisonService,
        DeterministicTextEquivalenceService equivalenceService)
    {
        _textComparisonService = textComparisonService;
        _equivalenceService = equivalenceService;
    }

    public CorrectionOrchestrationResult CompareTexts(
        string originalText,
        string userText,
        bool isPro)
    {
        var staticResult = _textComparisonService.CompareTexts(originalText, userText);
        if (!isPro || staticResult.Comparisons.Count == 0)
        {
            return new CorrectionOrchestrationResult(
                staticResult,
                staticResult.Comparisons.Count,
                RemovedComparisonCount: 0);
        }

        var remainingComparisons = staticResult.Comparisons
            .Where(comparison => !_equivalenceService.AreEquivalent(
                comparison.OriginalText,
                comparison.UserText))
            .ToList();

        if (remainingComparisons.Count == staticResult.Comparisons.Count)
        {
            return new CorrectionOrchestrationResult(
                staticResult,
                staticResult.Comparisons.Count,
                RemovedComparisonCount: 0);
        }

        var normalizedResult = new TextComparisonResult(
            staticResult.OriginalText,
            staticResult.UserText,
            CalculateAccuracy(staticResult.OriginalText, remainingComparisons),
            remainingComparisons,
            CorrectionModes.Normalized);

        return new CorrectionOrchestrationResult(
            normalizedResult,
            staticResult.Comparisons.Count,
            staticResult.Comparisons.Count - remainingComparisons.Count);
    }

    private static double CalculateAccuracy(
        string originalText,
        IReadOnlyCollection<TextComparison> comparisons)
    {
        if (originalText.Length == 0)
        {
            return 0;
        }

        var comparisonsLength = comparisons.Sum(comparison => comparison.OriginalText?.Length ?? 0);
        return 1 - ((double)comparisonsLength / originalText.Length);
    }
}

public sealed record CorrectionOrchestrationResult(
    TextComparisonResult Result,
    int StaticComparisonCount,
    int RemovedComparisonCount);

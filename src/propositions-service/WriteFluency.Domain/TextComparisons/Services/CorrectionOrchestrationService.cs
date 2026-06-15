namespace WriteFluency.TextComparisons;

public sealed class CorrectionOrchestrationService
{
    private readonly TextComparisonService _textComparisonService;
    private readonly DeterministicTextEquivalenceService _equivalenceService;
    private readonly ITextComparisonAiRefiner _aiRefiner;
    private readonly AiRefinementOutputValidator _aiOutputValidator;

    public CorrectionOrchestrationService(
        TextComparisonService textComparisonService,
        DeterministicTextEquivalenceService equivalenceService,
        ITextComparisonAiRefiner aiRefiner,
        AiRefinementOutputValidator aiOutputValidator)
    {
        _textComparisonService = textComparisonService;
        _equivalenceService = equivalenceService;
        _aiRefiner = aiRefiner;
        _aiOutputValidator = aiOutputValidator;
    }

    public async Task<CorrectionOrchestrationResult> CompareTextsAsync(
        string originalText,
        string userText,
        bool isPro,
        CancellationToken cancellationToken)
    {
        var staticResult = _textComparisonService.CompareTexts(originalText, userText);
        if (!isPro)
        {
            return CreateResult(staticResult, staticResult.Comparisons.Count);
        }

        var remainingComparisons = staticResult.Comparisons
            .Where(comparison => !_equivalenceService.AreEquivalent(
                comparison.OriginalText,
                comparison.UserText))
            .ToList();

        var removedComparisonCount = staticResult.Comparisons.Count - remainingComparisons.Count;
        var preAiResult = removedComparisonCount == 0
            ? staticResult
            : new TextComparisonResult(
                staticResult.OriginalText,
                staticResult.UserText,
                CalculateAccuracy(staticResult.OriginalText, remainingComparisons),
                remainingComparisons,
                CorrectionModes.Normalized);

        if (preAiResult.Comparisons.Count == 0)
        {
            return CreateResult(
                preAiResult,
                staticResult.Comparisons.Count,
                removedComparisonCount);
        }

        var request = CreateAiRequest(preAiResult);

        try
        {
            var refinement = await _aiRefiner.RefineAsync(request, cancellationToken);
            var validation = _aiOutputValidator.Validate(request, refinement.Comparisons);

            if (!validation.IsValid)
            {
                return CreateFallbackResult(
                    preAiResult,
                    staticResult.Comparisons.Count,
                    removedComparisonCount,
                    request.Comparisons.Count,
                    refinement,
                    validation.FailureReason);
            }

            var finalComparisons = validation.Comparisons.ToList();
            var aiRefinedResult = new TextComparisonResult(
                preAiResult.OriginalText,
                preAiResult.UserText,
                CalculateAccuracy(preAiResult.OriginalText, finalComparisons),
                finalComparisons,
                CorrectionModes.AiRefined,
                aiAttempted: true);

            return CreateResult(
                aiRefinedResult,
                staticResult.Comparisons.Count,
                removedComparisonCount,
                request.Comparisons.Count,
                finalComparisons.Count,
                refinement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return CreateFallbackResult(
                preAiResult,
                staticResult.Comparisons.Count,
                removedComparisonCount,
                request.Comparisons.Count,
                null,
                null);
        }
    }

    private static AiRefinementRequest CreateAiRequest(TextComparisonResult preAiResult)
    {
        var comparisons = preAiResult.Comparisons
            .Select((comparison, index) => new AiRefinementSourceComparison(
                index,
                comparison.OriginalTextRange,
                comparison.OriginalText ?? string.Empty,
                comparison.UserTextRange,
                comparison.UserText ?? string.Empty))
            .ToList();

        return new AiRefinementRequest(
            preAiResult.OriginalText,
            preAiResult.UserText,
            comparisons);
    }

    private CorrectionOrchestrationResult CreateFallbackResult(
        TextComparisonResult preAiResult,
        int staticComparisonCount,
        int removedComparisonCount,
        int aiInputComparisonCount,
        AiRefinementResult? refinement,
        string? validationFailureReason)
    {
        var fallbackResult = new TextComparisonResult(
            preAiResult.OriginalText,
            preAiResult.UserText,
            preAiResult.AccuracyPercentage,
            preAiResult.Comparisons,
            CorrectionModes.Fallback,
            aiAttempted: true);

        return CreateResult(
            fallbackResult,
            staticComparisonCount,
            removedComparisonCount,
            aiInputComparisonCount,
            refinement?.Comparisons.Count ?? 0,
            refinement,
            validationFailureReason);
    }

    private CorrectionOrchestrationResult CreateResult(
        TextComparisonResult result,
        int staticComparisonCount,
        int removedComparisonCount = 0,
        int aiInputComparisonCount = 0,
        int aiOutputComparisonCount = 0,
        AiRefinementResult? refinement = null,
        string? validationFailureReason = null) =>
        new(
            result,
            staticComparisonCount,
            removedComparisonCount,
            aiInputComparisonCount,
            aiOutputComparisonCount,
            refinement?.DurationMilliseconds,
            refinement?.InputTokenCount,
            refinement?.OutputTokenCount,
            result.AiAttempted ? _aiRefiner.Model : null,
            result.AiAttempted ? _aiRefiner.PromptVersion : null,
            validationFailureReason);

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
    int RemovedComparisonCount,
    int AiInputComparisonCount,
    int AiOutputComparisonCount,
    long? AiDurationMilliseconds,
    long? AiInputTokenCount,
    long? AiOutputTokenCount,
    string? AiModel,
    string? AiPromptVersion,
    string? AiValidationFailureReason);

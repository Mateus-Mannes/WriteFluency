namespace WriteFluency.TextComparisons;

public sealed class CorrectionOrchestrationService
{
    private readonly TextComparisonService _textComparisonService;
    private readonly DeterministicTextComparisonRefiner _deterministicRefiner;
    private readonly TextComparisonRefinementValidator _validator;

    public CorrectionOrchestrationService(
        TextComparisonService textComparisonService,
        DeterministicTextComparisonRefiner deterministicRefiner,
        TextComparisonRefinementValidator validator)
    {
        _textComparisonService = textComparisonService;
        _deterministicRefiner = deterministicRefiner;
        _validator = validator;
    }

    public Task<CorrectionOrchestrationResult> CompareTextsAsync(
        string originalText,
        string userText,
        bool isPro,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var staticResult = _textComparisonService.CompareTexts(originalText, userText);
        AssignStaticProvenance(staticResult.Comparisons);
        var staticComparisonCount = staticResult.Comparisons.Count;
        var staticValidation = _validator.ValidateStatic(staticResult);
        if (!staticValidation.IsValid)
        {
            var sanitizedStaticResult = CreateStaticResult(
                staticResult.OriginalText,
                staticResult.UserText,
                staticValidation.Comparisons);

            return Task.FromResult(CreateResult(
                sanitizedStaticResult,
                staticComparisonCount,
                validationReasonCode: staticValidation.ReasonCode));
        }

        if (staticValidation.ShouldSkipRefinement)
        {
            return Task.FromResult(CreateResult(
                CreateStaticResult(
                    staticResult.OriginalText,
                    staticResult.UserText,
                    staticValidation.Comparisons),
                staticComparisonCount,
                validationReasonCode: staticValidation.ReasonCode));
        }

        if (!isPro)
        {
            return Task.FromResult(CreateResult(
                CreateStaticResult(
                    staticResult.OriginalText,
                    staticResult.UserText,
                    staticValidation.Comparisons),
                staticComparisonCount,
                validationReasonCode: staticValidation.ReasonCode));
        }

        var deterministic = _deterministicRefiner.Refine(
            staticResult.OriginalText,
            staticResult.UserText,
            staticValidation.Comparisons);
        var correctionTrace = deterministic.Trace.ToDictionary(
            entry => entry.Key,
            entry => entry.Value);
        var deterministicValidation = _validator.ValidateFinal(
            staticResult.OriginalText,
            staticResult.UserText,
            deterministic.Comparisons);
        if (!deterministicValidation.IsValid)
        {
            return Task.FromResult(CreateResult(
                CreateStaticResult(
                    staticResult.OriginalText,
                    staticResult.UserText,
                    staticValidation.Comparisons),
                staticComparisonCount,
                validationReasonCode: deterministicValidation.ReasonCode));
        }

        if (!deterministic.HasChanges)
        {
            return Task.FromResult(CreateResult(
                CreateStaticResult(
                    staticResult.OriginalText,
                    staticResult.UserText,
                    staticValidation.Comparisons),
                staticComparisonCount,
                validationReasonCode: deterministicValidation.ReasonCode));
        }

        var normalizedResult = new TextComparisonResult(
            staticResult.OriginalText,
            staticResult.UserText,
            CalculateAccuracy(staticResult.OriginalText, deterministicValidation.Comparisons),
            deterministicValidation.Comparisons.ToList(),
            CorrectionModes.Normalized,
            correctionTrace: OrderTrace(correctionTrace));

        return Task.FromResult(CreateResult(
            normalizedResult,
            staticComparisonCount,
            deterministic.RemovedComparisonCount,
            deterministicValidation.ReasonCode));
    }

    private CorrectionOrchestrationResult CreateResult(
        TextComparisonResult result,
        int staticComparisonCount,
        int removedComparisonCount = 0,
        string validationReasonCode =
            TextComparisonRefinementValidationReasons.Valid) =>
        new(
            result,
            staticComparisonCount,
            removedComparisonCount,
            validationReasonCode);

    private static TextComparisonResult CreateStaticResult(
        string originalText,
        string userText,
        IReadOnlyList<TextComparison> comparisons) =>
        new(
            originalText,
            userText,
            CalculateAccuracy(originalText, comparisons),
            comparisons.ToList());

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

    private static void AssignStaticProvenance(
        IReadOnlyList<TextComparison> comparisons)
    {
        for (var index = 0; index < comparisons.Count; index++)
        {
            comparisons[index].SourceComparisonIndex = index;
            comparisons[index].IsDeterministicallyRefined = false;
        }
    }

    private static IReadOnlyList<CorrectionTraceEntry>? OrderTrace(
        IReadOnlyDictionary<int, CorrectionTraceEntry> trace) =>
        trace.Count == 0
            ? null
            : trace.Values
                .OrderBy(entry => entry.SourceComparisonIndex)
                .ToList();
}

public sealed record CorrectionOrchestrationResult(
    TextComparisonResult Result,
    int StaticComparisonCount,
    int RemovedComparisonCount,
    string ValidationReasonCode);

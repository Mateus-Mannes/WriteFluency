namespace WriteFluency.TextComparisons;

public sealed class CorrectionOrchestrationService
{
    private readonly TextComparisonService _textComparisonService;
    private readonly DeterministicTextComparisonRefiner _deterministicRefiner;

    public CorrectionOrchestrationService(
        TextComparisonService textComparisonService,
        DeterministicTextComparisonRefiner deterministicRefiner)
    {
        _textComparisonService = textComparisonService;
        _deterministicRefiner = deterministicRefiner;
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

        if (!isPro)
        {
            return Task.FromResult(CreateResult(
                staticResult,
                staticResult.Comparisons.Count));
        }

        var deterministic = _deterministicRefiner.Refine(
            staticResult.OriginalText,
            staticResult.UserText,
            staticResult.Comparisons);
        var correctionTrace = deterministic.Trace.ToDictionary(
            entry => entry.Key,
            entry => entry.Value);

        if (!deterministic.HasChanges)
        {
            return Task.FromResult(CreateResult(
                staticResult,
                staticResult.Comparisons.Count));
        }

        var normalizedResult = new TextComparisonResult(
            staticResult.OriginalText,
            staticResult.UserText,
            CalculateAccuracy(staticResult.OriginalText, deterministic.Comparisons),
            deterministic.Comparisons.ToList(),
            CorrectionModes.Normalized,
            correctionTrace: OrderTrace(correctionTrace));

        return Task.FromResult(CreateResult(
            normalizedResult,
            staticResult.Comparisons.Count,
            deterministic.RemovedComparisonCount));
    }

    private CorrectionOrchestrationResult CreateResult(
        TextComparisonResult result,
        int staticComparisonCount,
        int removedComparisonCount = 0) =>
        new(
            result,
            staticComparisonCount,
            removedComparisonCount);

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
            comparisons[index].IsAiRefined = false;
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
    int RemovedComparisonCount);

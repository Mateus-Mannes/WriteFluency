namespace WriteFluency.TextComparisons;

public sealed class CorrectionOrchestrationService
{
    private readonly TextComparisonService _textComparisonService;
    private readonly DeterministicTextComparisonRefiner _deterministicRefiner;
    private readonly IMistakePatternClassifier _mistakePatternClassifier;

    public CorrectionOrchestrationService(
        TextComparisonService textComparisonService,
        DeterministicTextComparisonRefiner deterministicRefiner,
        IMistakePatternClassifier mistakePatternClassifier)
    {
        _textComparisonService = textComparisonService;
        _deterministicRefiner = deterministicRefiner;
        _mistakePatternClassifier = mistakePatternClassifier;
    }

    public async Task<CorrectionOrchestrationResult> CompareTextsAsync(
        string originalText,
        string userText,
        bool isPro,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var staticResult = _textComparisonService.CompareTexts(originalText, userText);
        AssignStaticProvenance(staticResult.Comparisons);
        var staticComparisonCount = staticResult.Comparisons.Count;
        if (!isPro)
        {
            return CreateResult(
                CreateStaticResult(
                    staticResult.OriginalText,
                    staticResult.UserText,
                    staticResult.Comparisons),
                staticComparisonCount);
        }

        TextComparisonStructureGuard.EnsureValid(staticResult);
        var deterministic = _deterministicRefiner.Refine(
            staticResult.OriginalText,
            staticResult.UserText,
            staticResult.Comparisons);
        var correctionTrace = deterministic.Trace.ToDictionary(
            entry => entry.Key,
            entry => entry.Value);

        if (!deterministic.HasChanges)
        {
            var unchangedResult = CreateStaticResult(
                staticResult.OriginalText,
                staticResult.UserText,
                staticResult.Comparisons);
            await AttachMistakePatternMetadataAsync(
                unchangedResult,
                cancellationToken);

            return CreateResult(
                unchangedResult,
                staticComparisonCount);
        }

        var normalizedResult = new TextComparisonResult(
            staticResult.OriginalText,
            staticResult.UserText,
            CalculateAccuracy(staticResult.OriginalText, deterministic.Comparisons),
            deterministic.Comparisons.ToList(),
            CorrectionModes.Normalized,
            correctionTrace: OrderTrace(correctionTrace));
        await AttachMistakePatternMetadataAsync(normalizedResult, cancellationToken);

        return CreateResult(
            normalizedResult,
            staticComparisonCount,
            deterministic.RemovedComparisonCount);
    }

    private async Task AttachMistakePatternMetadataAsync(
        TextComparisonResult result,
        CancellationToken cancellationToken)
    {
        TextComparisonStructureGuard.EnsureValid(result);
        TextComparisonStructureGuard.EnsureValidSourceIndexes(result.Comparisons);

        if (result.Comparisons.Count == 0)
        {
            return;
        }

        try
        {
            var annotations = await _mistakePatternClassifier.ClassifyAsync(
                new MistakePatternClassificationRequest(
                    result.OriginalText,
                    result.UserText,
                    result.Comparisons),
                cancellationToken);

            AttachMistakePatternMetadata(
                result.Comparisons,
                MistakePatternAnnotationSanitizer.Sanitize(
                    annotations,
                    result.Comparisons));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            ClearMistakePatternMetadata(result.Comparisons);
        }
    }

    private static void AttachMistakePatternMetadata(
        IReadOnlyList<TextComparison> comparisons,
        IReadOnlyList<MistakePatternAnnotation>? annotations)
    {
        ClearMistakePatternMetadata(comparisons);
        if (annotations is null || annotations.Count == 0)
        {
            return;
        }

        foreach (var annotation in annotations)
        {
            if (annotation.ComparisonIndex < 0
                || annotation.ComparisonIndex >= comparisons.Count)
            {
                continue;
            }

            var comparison = comparisons[annotation.ComparisonIndex];
            if (comparison.SourceComparisonIndex != annotation.SourceComparisonIndex)
            {
                continue;
            }

            comparison.MistakePatternTags = annotation.Tags;
            comparison.MistakePatternPhrase = annotation.StudentPhrase;
        }
    }

    private static void ClearMistakePatternMetadata(
        IReadOnlyList<TextComparison> comparisons)
    {
        foreach (var comparison in comparisons)
        {
            comparison.MistakePatternTags = null;
            comparison.MistakePatternPhrase = null;
        }
    }

    private CorrectionOrchestrationResult CreateResult(
        TextComparisonResult result,
        int staticComparisonCount,
        int removedComparisonCount = 0) =>
        new(
            result,
            staticComparisonCount,
            removedComparisonCount);

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
    int RemovedComparisonCount);

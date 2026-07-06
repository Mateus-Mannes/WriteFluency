namespace WriteFluency.TextComparisons;

public sealed class CorrectionOrchestrationService
{
    private readonly TextComparisonService _textComparisonService;
    private readonly DeterministicTextComparisonRefiner _deterministicRefiner;
    private readonly IMistakePatternClassifier _mistakePatternClassifier;
    private readonly IAiUsageLimiter _aiUsageLimiter;

    public CorrectionOrchestrationService(
        TextComparisonService textComparisonService,
        DeterministicTextComparisonRefiner deterministicRefiner,
        IMistakePatternClassifier mistakePatternClassifier,
        IAiUsageLimiter aiUsageLimiter)
    {
        _textComparisonService = textComparisonService;
        _deterministicRefiner = deterministicRefiner;
        _mistakePatternClassifier = mistakePatternClassifier;
        _aiUsageLimiter = aiUsageLimiter;
    }

    public async Task<CorrectionOrchestrationResult> CompareTextsAsync(
        string originalText,
        string userText,
        bool isPro,
        CancellationToken cancellationToken) =>
        await CompareTextsAsync(
            originalText,
            userText,
            isPro,
            userId: null,
            cancellationToken);

    public async Task<CorrectionOrchestrationResult> CompareTextsAsync(
        string originalText,
        string userText,
        bool isPro,
        string? userId = null,
        CancellationToken cancellationToken = default)
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
                userId,
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
        await AttachMistakePatternMetadataAsync(normalizedResult, userId, cancellationToken);

        return CreateResult(
            normalizedResult,
            staticComparisonCount,
            deterministic.RemovedComparisonCount);
    }

    private async Task AttachMistakePatternMetadataAsync(
        TextComparisonResult result,
        string? userId,
        CancellationToken cancellationToken)
    {
        TextComparisonStructureGuard.EnsureValid(result);
        TextComparisonStructureGuard.EnsureValidSourceIndexes(result.Comparisons);

        if (result.Comparisons.Count == 0)
        {
            result.MistakePatternStatus = MistakePatternStatuses.NotApplicable;
            return;
        }

        if (!_mistakePatternClassifier.IsEnabled)
        {
            ClearMistakePatternMetadata(result.Comparisons);
            result.MistakePatternStatus = MistakePatternStatuses.SkippedDisabled;
            result.MistakePatternMessage = "The Pro AI review is currently disabled.";
            return;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            ClearMistakePatternMetadata(result.Comparisons);
            result.MistakePatternStatus = MistakePatternStatuses.SkippedUsageLimit;
            result.MistakePatternMessage = "The Pro AI review could not run because your session could not be verified.";
            return;
        }

        var reservation = await _aiUsageLimiter.TryReserveAsync(
            new AiUsageReservationRequest(
                userId,
                AiUsageFeatures.MistakePatternClassification),
            cancellationToken);

        if (!reservation.IsAllowed)
        {
            ClearMistakePatternMetadata(result.Comparisons);
            result.MistakePatternStatus = MistakePatternStatuses.SkippedUsageLimit;
            result.MistakePatternMessage = CreateUsageLimitMessage(reservation.DenialReason);
            return;
        }

        try
        {
            var classificationRun = await _mistakePatternClassifier.ClassifyWithDiagnosticsAsync(
                new MistakePatternClassificationRequest(
                    result.OriginalText,
                    result.UserText,
                    result.Comparisons),
                cancellationToken);

            AttachMistakePatternMetadata(
                result.Comparisons,
                MistakePatternAnnotationSanitizer.Sanitize(
                    classificationRun.Annotations,
                    result.Comparisons));
            await _aiUsageLimiter.RecordCompletionAsync(
                reservation,
                new AiUsageCompletion(
                    classificationRun.InputTokenCount,
                    classificationRun.OutputTokenCount),
                cancellationToken);

            if (result.Comparisons.Any(comparison =>
                    comparison.MistakePatternTags?.Count > 0
                    && !string.IsNullOrWhiteSpace(comparison.MistakePatternPhrase)))
            {
                result.MistakePatternStatus = MistakePatternStatuses.Generated;
                result.MistakePatternMessage = null;
            }
            else
            {
                result.MistakePatternStatus = MistakePatternStatuses.SkippedDisabled;
                result.MistakePatternMessage = "The Pro AI review is currently disabled.";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await _aiUsageLimiter.RecordFailureAsync(reservation, cancellationToken);
            ClearMistakePatternMetadata(result.Comparisons);
            result.MistakePatternStatus = MistakePatternStatuses.ClassifierFailed;
            result.MistakePatternMessage = "The Pro AI review is temporarily unavailable. Your correction highlights are still available.";
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

    private static string CreateUsageLimitMessage(string? denialReason) =>
        denialReason switch
        {
            "daily_limit_exceeded" =>
                "You reached today's Pro AI review limit. Your correction highlights are still available; only the AI mistake-pattern review is paused. You can use AI review again tomorrow. If this seems unexpected, contact us on the Support page.",
            "monthly_limit_exceeded" =>
                "You reached this month's Pro AI review limit. Your correction highlights are still available; only the AI mistake-pattern review is paused. You can use AI review again when the monthly limit resets. If this seems unexpected, contact us on the Support page.",
            "monthly_cost_limit_exceeded" =>
                "Your Pro AI review is paused because this month's estimated AI usage limit was reached. This helps keep the Pro plan affordable. Your correction highlights are still available, and AI review will be available again when the monthly limit resets. If this seems unexpected, contact us on the Support page.",
            _ =>
                "Your Pro AI review limit was reached. Your correction highlights are still available; only the AI mistake-pattern review is paused. Please try again later, or contact us on the Support page if this seems unexpected."
        };

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

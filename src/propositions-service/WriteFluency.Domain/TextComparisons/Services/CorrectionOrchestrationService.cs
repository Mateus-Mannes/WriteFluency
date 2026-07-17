namespace WriteFluency.TextComparisons;

public sealed class CorrectionOrchestrationService
{
    private readonly TextComparisonService _textComparisonService;
    private readonly DeterministicTextComparisonRefiner _deterministicRefiner;
    private readonly IMistakePatternClassifier _mistakePatternClassifier;
    private readonly IAiUsageLimiter _aiUsageLimiter;
    private readonly ProReviewEligibilityService _proReviewEligibilityService;

    public CorrectionOrchestrationService(
        TextComparisonService textComparisonService,
        DeterministicTextComparisonRefiner deterministicRefiner,
        IMistakePatternClassifier mistakePatternClassifier,
        IAiUsageLimiter aiUsageLimiter,
        ProReviewEligibilityService proReviewEligibilityService)
    {
        _textComparisonService = textComparisonService;
        _deterministicRefiner = deterministicRefiner;
        _mistakePatternClassifier = mistakePatternClassifier;
        _aiUsageLimiter = aiUsageLimiter;
        _proReviewEligibilityService = proReviewEligibilityService;
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
        CancellationToken cancellationToken = default) =>
        await CompareTextsAsync(
            new CorrectionOrchestrationRequest(
                originalText,
                userText,
                IsAuthenticated: !string.IsNullOrWhiteSpace(userId),
                isPro,
                userId,
                AnonymousFingerprintHash: null,
                AnonymousClientIpAddress: null,
                EnableFreeReviewTeaser: false),
            cancellationToken);

    public async Task<CorrectionOrchestrationResult> CompareTextsAsync(
        CorrectionOrchestrationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var staticResult = _textComparisonService.CompareTexts(request.OriginalText, request.UserText);
        AssignStaticProvenance(staticResult.Comparisons);
        var staticComparisonCount = staticResult.Comparisons.Count;
        if ((!request.IsPro && !request.EnableFreeReviewTeaser) || string.IsNullOrWhiteSpace(request.UserText))
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

        if (!deterministic.HasChanges)
        {
            var unchangedResult = CreateStaticResult(
                staticResult.OriginalText,
                staticResult.UserText,
                staticResult.Comparisons);
            await AttachMistakePatternMetadataAsync(
                unchangedResult,
                request,
                cancellationToken);

            return CreateResult(
                unchangedResult,
                staticComparisonCount);
        }

        var correctionTrace = deterministic.Trace.ToDictionary(
            entry => entry.Key,
            entry => entry.Value);
        var normalizedResult = new TextComparisonResult(
            staticResult.OriginalText,
            staticResult.UserText,
            CalculateAccuracy(staticResult.OriginalText, deterministic.Comparisons),
            deterministic.Comparisons.ToList(),
            CorrectionModes.Normalized,
            correctionTrace: OrderTrace(correctionTrace));
        await AttachMistakePatternMetadataAsync(normalizedResult, request, cancellationToken);

        return CreateResult(
            normalizedResult,
            staticComparisonCount,
            deterministic.RemovedComparisonCount);
    }

    private async Task AttachMistakePatternMetadataAsync(
        TextComparisonResult result,
        CorrectionOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        TextComparisonStructureGuard.EnsureValid(result);
        TextComparisonStructureGuard.EnsureValidSourceIndexes(result.Comparisons);

        if (result.Comparisons.Count == 0)
        {
            result.MistakePatternStatus = MistakePatternStatuses.NotApplicable;
            result.MistakePatternReviewSource = MistakePatternReviewSources.None;
            return;
        }

        if (!_mistakePatternClassifier.IsEnabled)
        {
            ClearMistakePatternMetadata(result.Comparisons);
            result.MistakePatternStatus = MistakePatternStatuses.SkippedDisabled;
            result.MistakePatternMessage = "The Pro AI review is currently disabled.";
            result.MistakePatternReviewSource = MistakePatternReviewSources.None;
            return;
        }

        var decision = await _proReviewEligibilityService.DecideAsync(
            request,
            cancellationToken);
        if (decision.Kind != ProReviewDecisionKind.FullProReview
            || decision.Reservation is null)
        {
            ClearMistakePatternMetadata(result.Comparisons);
            result.MistakePatternStatus = decision.MistakePatternStatus;
            result.MistakePatternMessage = decision.MistakePatternMessage;
            result.MistakePatternReviewSource = MistakePatternReviewSources.None;
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
                decision.Reservation,
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
                result.MistakePatternReviewSource = decision.MistakePatternReviewSource;
            }
            else
            {
                result.MistakePatternStatus = MistakePatternStatuses.SkippedDisabled;
                result.MistakePatternMessage = "The Pro AI review is currently disabled.";
                result.MistakePatternReviewSource = MistakePatternReviewSources.None;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await _aiUsageLimiter.RecordFailureAsync(decision.Reservation, cancellationToken);
            ClearMistakePatternMetadata(result.Comparisons);
            result.MistakePatternStatus = MistakePatternStatuses.ClassifierFailed;
            result.MistakePatternMessage = "The Pro AI review is temporarily unavailable. Your correction highlights are still available.";
            result.MistakePatternReviewSource = MistakePatternReviewSources.None;
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

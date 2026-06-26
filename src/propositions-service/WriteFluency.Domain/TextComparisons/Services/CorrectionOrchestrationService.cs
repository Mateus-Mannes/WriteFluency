namespace WriteFluency.TextComparisons;

public sealed class CorrectionOrchestrationService
{
    private readonly TextComparisonService _textComparisonService;
    private readonly DeterministicTextComparisonRefiner _deterministicRefiner;
    private readonly ITextComparisonAiRefiner _aiRefiner;
    private readonly AiRefinementOutputValidator _aiOutputValidator;

    public CorrectionOrchestrationService(
        TextComparisonService textComparisonService,
        DeterministicTextComparisonRefiner deterministicRefiner,
        ITextComparisonAiRefiner aiRefiner,
        AiRefinementOutputValidator aiOutputValidator)
    {
        _textComparisonService = textComparisonService;
        _deterministicRefiner = deterministicRefiner;
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
        AssignStaticProvenance(staticResult.Comparisons);

        if (!isPro)
        {
            return CreateResult(staticResult, staticResult.Comparisons.Count);
        }

        var deterministic = _deterministicRefiner.Refine(
            staticResult.OriginalText,
            staticResult.UserText,
            staticResult.Comparisons);
        var correctionTrace = deterministic.Trace.ToDictionary(
            entry => entry.Key,
            entry => entry.Value);

        var preAiResult = !deterministic.HasChanges
            ? staticResult
            : new TextComparisonResult(
                staticResult.OriginalText,
                staticResult.UserText,
                CalculateAccuracy(staticResult.OriginalText, deterministic.Comparisons),
                deterministic.Comparisons.ToList(),
                CorrectionModes.Normalized,
                correctionTrace: OrderTrace(correctionTrace));

        if (preAiResult.Comparisons.Count == 0)
        {
            return CreateResult(
                preAiResult,
                staticResult.Comparisons.Count,
                deterministic.RemovedComparisonCount);
        }

        var request = CreateAiRequest(preAiResult);

        try
        {
            var refinement = await _aiRefiner.RefineAsync(request, cancellationToken);
            var validation = _aiOutputValidator.ValidateDecisions(
                request,
                refinement.Decisions);

            if (!validation.IsValid)
            {
                return CreateFallbackResult(
                    preAiResult,
                    staticResult.Comparisons.Count,
                    deterministic.RemovedComparisonCount,
                    request.Comparisons.Count,
                    refinement,
                    validation.FailureReason);
            }

            var finalComparisons = ApplyDeterministicProvenance(
                validation.Comparisons,
                preAiResult.Comparisons);
            AddAiTrace(
                staticResult.Comparisons,
                request,
                validation.Decisions,
                correctionTrace);
            var aiRefinedResult = new TextComparisonResult(
                preAiResult.OriginalText,
                preAiResult.UserText,
                CalculateAccuracy(preAiResult.OriginalText, finalComparisons),
                finalComparisons,
                CorrectionModes.AiRefined,
                aiAttempted: true,
                correctionTrace: OrderTrace(correctionTrace));

            return CreateResult(
                aiRefinedResult,
                staticResult.Comparisons.Count,
                deterministic.RemovedComparisonCount,
                request.Comparisons.Count,
                finalComparisons.Count,
                refinement,
                validation.FailureReason,
                validation.RejectedSourceComparisonCount);
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
                deterministic.RemovedComparisonCount,
                request.Comparisons.Count,
                null,
                null);
        }
    }

    private static AiRefinementRequest CreateAiRequest(TextComparisonResult preAiResult)
    {
        var comparisons = preAiResult.Comparisons
            .Select(comparison => new AiRefinementSourceComparison(
                comparison.SourceComparisonIndex,
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
            aiAttempted: true,
            correctionTrace: preAiResult.CorrectionTrace);

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
        string? validationFailureReason = null,
        int aiRejectedSourceComparisonCount = 0) =>
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
            validationFailureReason,
            aiRejectedSourceComparisonCount);

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

    private static List<TextComparison> ApplyDeterministicProvenance(
        IReadOnlyList<TextComparison> comparisons,
        IReadOnlyList<TextComparison> preAiComparisons)
    {
        var deterministicSourceIndexes = preAiComparisons
            .Where(comparison => comparison.IsDeterministicallyRefined)
            .Select(comparison => comparison.SourceComparisonIndex)
            .ToHashSet();

        foreach (var comparison in comparisons)
        {
            if (deterministicSourceIndexes.Contains(
                    comparison.SourceComparisonIndex))
            {
                comparison.IsDeterministicallyRefined = true;
            }
        }

        return comparisons.ToList();
    }

    private static void AddAiTrace(
        IReadOnlyList<TextComparison> staticComparisons,
        AiRefinementRequest request,
        IReadOnlyList<AiRefinementDecisionValidation> decisions,
        IDictionary<int, CorrectionTraceEntry> trace)
    {
        var initialByIndex = staticComparisons.ToDictionary(
            comparison => comparison.SourceComparisonIndex);

        foreach (var decision in decisions.Where(decision =>
                     decision.ValidationStatus == "accepted"
                     && decision.IsEffectiveChange))
        {
            var initial = initialByIndex[decision.SourceComparisonIndex];
            var proposedOutput = ToProposedSnapshots(
                request,
                decision.ProposedRanges);
            var aiTrace = new CorrectionStageTrace(
                decision.Action,
                decision.ReasonCode,
                decision.OutputComparisons.Select(ToSnapshot).ToList(),
                decision.ValidationStatus,
                proposedOutput,
                decision.ValidationFailureReason);

            if (trace.TryGetValue(decision.SourceComparisonIndex, out var existing))
            {
                trace[decision.SourceComparisonIndex] = existing with { Ai = aiTrace };
            }
            else
            {
                trace[decision.SourceComparisonIndex] = new CorrectionTraceEntry(
                    decision.SourceComparisonIndex,
                    ToSnapshot(initial),
                    Ai: aiTrace);
            }
        }
    }

    private static IReadOnlyList<ComparisonSnapshot>? ToProposedSnapshots(
        AiRefinementRequest request,
        IReadOnlyList<AiRefinedComparison> ranges)
    {
        if (ranges.Count == 0)
        {
            return [];
        }

        var snapshots = new List<ComparisonSnapshot>(ranges.Count);
        foreach (var range in ranges)
        {
            if (!TrySnapshot(request, range, out var snapshot))
            {
                return null;
            }

            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    private static bool TrySnapshot(
        AiRefinementRequest request,
        AiRefinedComparison range,
        out ComparisonSnapshot snapshot)
    {
        snapshot = default!;
        if (!TrySlice(request.OriginalText, range.OriginalTextInitialIndex, range.OriginalTextFinalIndex, out var original)
            || !TrySlice(request.UserText, range.UserTextInitialIndex, range.UserTextFinalIndex, out var user))
        {
            return false;
        }

        snapshot = new ComparisonSnapshot(
            new TextRange(range.OriginalTextInitialIndex, range.OriginalTextFinalIndex),
            original,
            new TextRange(range.UserTextInitialIndex, range.UserTextFinalIndex),
            user);
        return true;
    }

    private static bool TrySlice(
        string text,
        int initialIndex,
        int finalIndex,
        out string value)
    {
        if (initialIndex < 0
            || finalIndex < initialIndex
            || finalIndex >= text.Length)
        {
            value = string.Empty;
            return false;
        }

        value = text.Substring(initialIndex, finalIndex - initialIndex + 1);
        return true;
    }

    private static ComparisonSnapshot ToSnapshot(TextComparison comparison) =>
        new(
            comparison.OriginalTextRange,
            comparison.OriginalText ?? string.Empty,
            comparison.UserTextRange,
            comparison.UserText ?? string.Empty);

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
    int AiInputComparisonCount,
    int AiOutputComparisonCount,
    long? AiDurationMilliseconds,
    long? AiInputTokenCount,
    long? AiOutputTokenCount,
    string? AiModel,
    string? AiPromptVersion,
    string? AiValidationFailureReason,
    int AiRejectedSourceComparisonCount);

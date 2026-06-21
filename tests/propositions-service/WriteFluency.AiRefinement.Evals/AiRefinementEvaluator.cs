using WriteFluency.TextComparisons;

namespace WriteFluency.AiRefinement.Evals;

public sealed class AiRefinementEvaluator
{
    private const double RequiredRemovalRecall = 0.80;
    private const double RequiredMeanSpanF1 = 0.85;
    private const double RequiredExactPassRate = 0.80;

    private readonly ITextComparisonAiRefiner _refiner;
    private readonly AiRefinementOutputValidator _validator;

    public AiRefinementEvaluator(
        ITextComparisonAiRefiner refiner,
        AiRefinementOutputValidator validator)
    {
        _refiner = refiner;
        _validator = validator;
    }

    public async Task<EvaluationSummary> EvaluateAsync(
        IReadOnlyList<EvaluationCase> cases,
        int runs,
        int concurrency,
        CancellationToken cancellationToken)
    {
        var workItems = Enumerable.Range(1, runs)
            .SelectMany(run => cases.Select((evaluationCase, caseIndex) =>
                new EvaluationWorkItem(
                    ((run - 1) * cases.Count) + caseIndex,
                    run,
                    evaluationCase)))
            .ToList();
        var results = new EvaluationCaseResult[workItems.Count];
        var completedCount = 0;

        await Parallel.ForEachAsync(
            workItems,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = concurrency,
                CancellationToken = cancellationToken
            },
            async (workItem, token) =>
            {
                Console.WriteLine(
                    $"[{workItem.Run}/{runs}] Evaluating {workItem.Case.CaseId}...");
                results[workItem.ResultIndex] = await EvaluateCaseAsync(
                    workItem.Case,
                    workItem.Run,
                    token);

                var completed = Interlocked.Increment(ref completedCount);
                Console.WriteLine(
                    $"Completed {completed}/{workItems.Count}: {workItem.Case.CaseId}");
            });

        return CreateSummary(results);
    }

    private sealed record EvaluationWorkItem(
        int ResultIndex,
        int Run,
        EvaluationCase Case);

    private async Task<EvaluationCaseResult> EvaluateCaseAsync(
        EvaluationCase evaluationCase,
        int runNumber,
        CancellationToken cancellationToken)
    {
        var sourceComparisons = evaluationCase.GetSourceComparisons()
            .Select(source => source.ToDomain())
            .ToList();
        var expectedDecisions = evaluationCase.GetExpectedDecisions();

        if (sourceComparisons.Any(source =>
                !TrySlice(
                    evaluationCase.OriginalText,
                    source.OriginalTextRange,
                    out var originalSnippet)
                || !TrySlice(
                    evaluationCase.UserText,
                    source.UserTextRange,
                    out var userSnippet)
                || originalSnippet != source.OriginalText
                || userSnippet != source.UserText))
        {
            return Failure(
                evaluationCase,
                runNumber,
                "Source comparison snippets do not match their ranges.");
        }

        var request = new AiRefinementRequest(
            evaluationCase.OriginalText,
            evaluationCase.UserText,
            sourceComparisons);

        try
        {
            var refinement = await _refiner.RefineAsync(request, cancellationToken);
            var validation = _validator.ValidateDecisions(request, refinement.Decisions);
            var sourceResults = CreateSourceResults(
                sourceComparisons,
                expectedDecisions,
                refinement,
                validation);
            var expectedRanges = sourceResults
                .SelectMany(result => result.ExpectedRanges)
                .OrderBy(range => range.OriginalTextInitialIndex)
                .ThenBy(range => range.UserTextInitialIndex)
                .ToList();
            var actualRanges = sourceResults
                .SelectMany(result => result.ActualRanges)
                .OrderBy(range => range.OriginalTextInitialIndex)
                .ThenBy(range => range.UserTextInitialIndex)
                .ToList();
            var isFullyValid = validation.IsValid
                && sourceResults.All(result => result.IsSafe);

            return new EvaluationCaseResult(
                evaluationCase.CaseId,
                evaluationCase.Category,
                runNumber,
                evaluationCase.GetFocusSourceComparisonIndex(),
                SummarizeActions(sourceResults.Select(result => result.ExpectedAction)),
                SummarizeActions(sourceResults.Select(result => result.ActualAction)),
                isFullyValid,
                isFullyValid && sourceResults.All(result => result.IsExactMatch),
                isFullyValid ? sourceResults.Average(result => result.SpanF1) : 0,
                refinement.DurationMilliseconds,
                refinement.InputTokenCount,
                refinement.OutputTokenCount,
                validation.FailureReason,
                expectedRanges,
                actualRanges,
                sourceResults);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(
                evaluationCase,
                runNumber,
                exception.GetType().Name);
        }
    }

    private EvaluationSummary CreateSummary(
        IReadOnlyList<EvaluationCaseResult> results)
    {
        var sourceResults = results.SelectMany(result => result.Sources).ToList();
        var expectedRemovals = sourceResults.Count(result => result.ExpectedAction == "remove");
        var actualRemovals = sourceResults.Count(result => result.ActualAction == "remove");
        var correctRemovals = sourceResults.Count(result =>
            result.ExpectedAction == "remove"
            && result.ActualAction == "remove");
        var genuineErrorRemovals = sourceResults.Count(result =>
            result.ExpectedAction != "remove"
            && result.ActualAction == "remove");
        var exactPassCount = results.Count(result => result.IsExactMatch);
        var exactComparisonCount = sourceResults.Count(result => result.IsExactMatch);
        var focusResults = results
            .Select(result => result.Sources.Single(source =>
                source.SourceComparisonIndex
                == result.FocusSourceComparisonIndex))
            .ToList();
        var exactFocusComparisonCount =
            focusResults.Count(result => result.IsExactMatch);
        var safeComparisonCount = sourceResults.Count(result => result.IsSafe);
        var meanComparisonSpanF1 = sourceResults.Count == 0
            ? 0
            : sourceResults.Average(result => result.SpanF1);
        var flakyCaseCount = results
            .GroupBy(result => result.CaseId)
            .Count(group => group
                .Select(result => result.IsExactMatch)
                .Distinct()
                .Count() > 1);
        var invalidOutputCount = results.Count(result => !result.IsSafe && result.Error is not null);
        var modelFailureCount = results.Count(result =>
            !result.IsSafe
            && result.Error is not null
            && result.Error != "missing_comparisons"
            && result.Error != "unknown_source_comparison"
            && result.Error != "invalid_range"
            && result.Error != "range_outside_source"
            && result.Error != "crossing_ranges"
            && result.Error != "partial_word_range"
            && result.Error != "identical_selected_text"
            && result.Error != "empty_range_after_normalization"
            && result.Error != "unsafe_text_slice");

        var removalPrecision = actualRemovals == 0
            ? expectedRemovals == 0 ? 1 : 0
            : (double)correctRemovals / actualRemovals;
        var removalRecall = expectedRemovals == 0
            ? 1
            : (double)correctRemovals / expectedRemovals;
        var exactPassRate = results.Count == 0
            ? 0
            : (double)exactPassCount / results.Count;
        var meanSpanF1 = results.Count == 0
            ? 0
            : results.Average(result => result.SpanF1);

        var passed = invalidOutputCount == 0
            && modelFailureCount == 0
            && genuineErrorRemovals == 0
            && removalPrecision == 1
            && removalRecall >= RequiredRemovalRecall
            && meanSpanF1 >= RequiredMeanSpanF1
            && exactPassRate >= RequiredExactPassRate;

        return new EvaluationSummary(
            _refiner.Model,
            _refiner.PromptVersion,
            DateTimeOffset.UtcNow,
            results.Select(result => result.RunNumber).DefaultIfEmpty().Max(),
            results.Select(result => result.CaseId).Distinct().Count(),
            results.Count,
            exactPassCount,
            exactPassRate,
            sourceResults.Count,
            exactComparisonCount,
            CalculateRate(exactComparisonCount, sourceResults.Count),
            focusResults.Count,
            exactFocusComparisonCount,
            CalculateRate(exactFocusComparisonCount, focusResults.Count),
            safeComparisonCount,
            meanComparisonSpanF1,
            flakyCaseCount,
            invalidOutputCount,
            modelFailureCount,
            genuineErrorRemovals,
            removalPrecision,
            removalRecall,
            meanSpanF1,
            results.Sum(result => result.InputTokenCount ?? 0),
            results.Sum(result => result.OutputTokenCount ?? 0),
            results.Sum(result => result.DurationMilliseconds),
            passed,
            results);
    }

    private static EvaluationCaseResult Failure(
        EvaluationCase evaluationCase,
        int runNumber,
        string error) =>
        new(
            evaluationCase.CaseId,
            evaluationCase.Category,
            runNumber,
            evaluationCase.GetFocusSourceComparisonIndex(),
            SummarizeActions(evaluationCase.GetExpectedDecisions()
                .Select(decision => decision.ExpectedAction)),
            "error",
            IsSafe: false,
            IsExactMatch: false,
            SpanF1: 0,
            DurationMilliseconds: 0,
            InputTokenCount: null,
            OutputTokenCount: null,
            Error: error,
            evaluationCase.GetExpectedDecisions()
                .SelectMany(decision => decision.ExpectedRanges)
                .ToList(),
            ActualRanges: [],
            Sources: evaluationCase.GetExpectedDecisions()
                .Select(decision => new EvaluationSourceResult(
                    decision.SourceComparisonIndex,
                    decision.ExpectedAction,
                    "error",
                    IsSafe: false,
                    IsExactMatch: false,
                    SpanF1: 0,
                    Error: error,
                    decision.ExpectedRanges,
                    ActualRanges: []))
                .ToList());

    private static double CalculateRate(int numerator, int denominator) =>
        denominator == 0 ? 0 : (double)numerator / denominator;

    private static IReadOnlyList<EvaluationSourceResult> CreateSourceResults(
        IReadOnlyList<AiRefinementSourceComparison> sources,
        IReadOnlyList<EvaluationExpectedDecision> expectedDecisions,
        AiRefinementResult refinement,
        AiRefinementDecisionValidationResult validation)
    {
        var expectedBySource = expectedDecisions.ToDictionary(
            decision => decision.SourceComparisonIndex);
        var proposedBySource = refinement.Decisions
            .GroupBy(decision => decision.SourceComparisonIndex)
            .ToDictionary(group => group.Key, group => group.First());
        var validatedBySource = validation.Decisions.ToDictionary(
            decision => decision.SourceComparisonIndex);

        return sources.Select(source =>
        {
            var expected = expectedBySource[source.SourceComparisonIndex];
            validatedBySource.TryGetValue(
                source.SourceComparisonIndex,
                out var validated);
            proposedBySource.TryGetValue(
                source.SourceComparisonIndex,
                out var proposed);

            var isSafe = validation.IsValid
                && validated is not null
                && validated.ValidationStatus == "accepted";
            var actualRanges = isSafe
                ? validated!.OutputComparisons.Select(ToRange).ToList()
                : proposed?.Comparisons.ToList() ?? [];
            var actualAction = isSafe
                ? DetermineAction(actualRanges, source)
                : "error";
            var expectedRanges = expected.ExpectedRanges
                .OrderBy(range => range.OriginalTextInitialIndex)
                .ThenBy(range => range.UserTextInitialIndex)
                .ToList();
            var orderedActualRanges = actualRanges
                .OrderBy(range => range.OriginalTextInitialIndex)
                .ThenBy(range => range.UserTextInitialIndex)
                .ToList();

            return new EvaluationSourceResult(
                source.SourceComparisonIndex,
                expected.ExpectedAction,
                actualAction,
                isSafe,
                isSafe
                && expected.ExpectedAction == actualAction
                && expectedRanges.SequenceEqual(orderedActualRanges),
                isSafe ? CalculateSpanF1(expectedRanges, orderedActualRanges) : 0,
                validated?.ValidationFailureReason ?? validation.FailureReason,
                expectedRanges,
                orderedActualRanges);
        }).ToList();
    }

    private static string SummarizeActions(IEnumerable<string> actions)
    {
        var values = actions.Distinct(StringComparer.Ordinal).ToList();
        return values.Count == 1 ? values[0] : "mixed";
    }

    private static string DetermineAction(
        IReadOnlyList<AiRefinedComparison> ranges,
        AiRefinementSourceComparison source)
    {
        if (ranges.Count == 0)
        {
            return "remove";
        }

        if (ranges.Count > 1)
        {
            return "split";
        }

        var range = ranges[0];
        return range.OriginalTextInitialIndex == source.OriginalTextRange.InitialIndex
            && range.OriginalTextFinalIndex == source.OriginalTextRange.FinalIndex
            && range.UserTextInitialIndex == source.UserTextRange.InitialIndex
            && range.UserTextFinalIndex == source.UserTextRange.FinalIndex
                ? "keep"
                : "shrink";
    }

    private static AiRefinedComparison ToRange(TextComparison comparison) =>
        new(
            comparison.SourceComparisonIndex,
            comparison.OriginalTextRange.InitialIndex,
            comparison.OriginalTextRange.FinalIndex,
            comparison.UserTextRange.InitialIndex,
            comparison.UserTextRange.FinalIndex);

    private static double CalculateSpanF1(
        IReadOnlyList<AiRefinedComparison> expected,
        IReadOnlyList<AiRefinedComparison> actual)
    {
        if (expected.Count == 0 && actual.Count == 0)
        {
            return 1;
        }

        var originalF1 = CalculateIndexF1(
            expected.SelectMany(ToOriginalIndexes),
            actual.SelectMany(ToOriginalIndexes));
        var userF1 = CalculateIndexF1(
            expected.SelectMany(ToUserIndexes),
            actual.SelectMany(ToUserIndexes));

        return (originalF1 + userF1) / 2;
    }

    private static double CalculateIndexF1(
        IEnumerable<int> expectedIndexes,
        IEnumerable<int> actualIndexes)
    {
        var expected = expectedIndexes.ToHashSet();
        var actual = actualIndexes.ToHashSet();

        if (expected.Count == 0 && actual.Count == 0)
        {
            return 1;
        }

        if (expected.Count == 0 || actual.Count == 0)
        {
            return 0;
        }

        var intersection = expected.Intersect(actual).Count();
        var precision = (double)intersection / actual.Count;
        var recall = (double)intersection / expected.Count;

        return precision + recall == 0
            ? 0
            : 2 * precision * recall / (precision + recall);
    }

    private static IEnumerable<int> ToOriginalIndexes(AiRefinedComparison range) =>
        Enumerable.Range(
            range.OriginalTextInitialIndex,
            range.OriginalTextFinalIndex - range.OriginalTextInitialIndex + 1);

    private static IEnumerable<int> ToUserIndexes(AiRefinedComparison range) =>
        Enumerable.Range(
            range.UserTextInitialIndex,
            range.UserTextFinalIndex - range.UserTextInitialIndex + 1);

    private static bool TrySlice(string text, TextRange range, out string snippet)
    {
        if (range.InitialIndex < 0
            || range.FinalIndex < range.InitialIndex
            || range.FinalIndex >= text.Length)
        {
            snippet = string.Empty;
            return false;
        }

        snippet = text.Substring(
            range.InitialIndex,
            range.FinalIndex - range.InitialIndex + 1);
        return snippet.Length > 0;
    }
}

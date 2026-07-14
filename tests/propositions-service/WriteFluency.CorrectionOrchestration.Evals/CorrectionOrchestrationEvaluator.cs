using WriteFluency.TextComparisons;

namespace WriteFluency.CorrectionOrchestration.Evals;

public sealed class CorrectionOrchestrationEvaluator
{
    private const double RequiredRemovalRecall = 0.80;
    private const double RequiredMeanSpanF1 = 0.85;
    private const double RequiredExactPassRate = 0.80;

    private readonly CorrectionOrchestrationService _orchestrationService;

    public CorrectionOrchestrationEvaluator(
        CorrectionOrchestrationService orchestrationService)
    {
        _orchestrationService = orchestrationService;
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
        if (evaluationCase.UsesOrchestrationContract)
        {
            return await EvaluateOrchestrationCaseAsync(
                evaluationCase,
                runNumber,
                cancellationToken);
        }

        return Failure(
            evaluationCase,
            runNumber,
            "legacy_refinement_cases_are_not_supported");
    }

    private async Task<EvaluationCaseResult> EvaluateOrchestrationCaseAsync(
        EvaluationCase evaluationCase,
        int runNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _orchestrationService.CompareTextsAsync(
                evaluationCase.OriginalText,
                evaluationCase.UserText,
                isPro: true,
                userId: "orchestration-evaluator",
                cancellationToken);
            var sourceResults = CreateOrchestrationSourceResults(
                evaluationCase,
                result);
            var expectedRanges = sourceResults
                .SelectMany(source => source.ExpectedRanges)
                .OrderBy(range => range.OriginalTextInitialIndex)
                .ThenBy(range => range.UserTextInitialIndex)
                .ToList();
            var actualRanges = sourceResults
                .SelectMany(source => source.ActualRanges)
                .OrderBy(range => range.OriginalTextInitialIndex)
                .ThenBy(range => range.UserTextInitialIndex)
                .ToList();
            var isSafe = sourceResults.All(source => source.IsSafe);

            return new EvaluationCaseResult(
                evaluationCase.CaseId,
                evaluationCase.Category,
                runNumber,
                evaluationCase.GetFocusSourceComparisonIndex(),
                SummarizeActions(sourceResults.Select(source =>
                    source.ExpectedAction)),
                SummarizeActions(sourceResults.Select(source =>
                    source.ActualAction)),
                isSafe,
                isSafe && sourceResults.All(source => source.IsExactMatch),
                isSafe ? sourceResults.Average(source => source.SpanF1) : 0,
                DurationMilliseconds: 0,
                InputTokenCount: null,
                OutputTokenCount: null,
                sourceResults.FirstOrDefault(source => !source.IsExactMatch)?.Error,
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
            "deterministic",
            "deterministic-text-comparison-refiner",
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

    private static IReadOnlyList<EvaluationSourceResult>
        CreateOrchestrationSourceResults(
            EvaluationCase evaluationCase,
            CorrectionOrchestrationResult orchestrationResult)
    {
        var sources = evaluationCase.GetSourceComparisons()
            .ToDictionary(source => source.SourceComparisonIndex);
        var expectedFinalBySource =
            (evaluationCase.ExpectedFinalComparisons ?? [])
            .GroupBy(comparison => comparison.SourceComparisonIndex)
            .ToDictionary(group => group.Key, group => group.ToList());
        var actualFinalBySource = orchestrationResult.Result.Comparisons
            .GroupBy(comparison => comparison.SourceComparisonIndex)
            .ToDictionary(group => group.Key, group => group.ToList());
        var expectedTraceBySource = (evaluationCase.ExpectedTrace ?? [])
            .ToDictionary(trace => trace.SourceComparisonIndex);
        var actualTraceBySource =
            (orchestrationResult.Result.CorrectionTrace ?? [])
            .ToDictionary(trace => trace.SourceComparisonIndex);

        return sources
            .OrderBy(source => source.Key)
            .Select(source =>
            {
                expectedFinalBySource.TryGetValue(source.Key, out var expectedFinal);
                actualFinalBySource.TryGetValue(source.Key, out var actualFinal);
                expectedTraceBySource.TryGetValue(source.Key, out var expectedTrace);
                actualTraceBySource.TryGetValue(source.Key, out var actualTrace);

                var expectedRanges = (expectedFinal ?? [])
                    .Select(comparison => comparison.ToRange())
                    .OrderBy(range => range.OriginalTextInitialIndex)
                    .ThenBy(range => range.UserTextInitialIndex)
                    .ToList();
                var actualRanges = (actualFinal ?? [])
                    .Select(ToRange)
                    .OrderBy(range => range.OriginalTextInitialIndex)
                    .ThenBy(range => range.UserTextInitialIndex)
                    .ToList();
                var finalMatches = ExpectedFinalMatches(
                    expectedFinal ?? [],
                    actualFinal ?? []);
                var traceMatches = ExpectedTraceMatches(
                    expectedTrace,
                    actualTrace);
                var isExact = finalMatches && traceMatches;

                return new EvaluationSourceResult(
                    source.Key,
                    DetermineAction(expectedRanges, source.Value),
                    DetermineAction(actualRanges, source.Value),
                    IsSafe: true,
                    isExact,
                    CalculateSpanF1(expectedRanges, actualRanges),
                    Error: isExact
                        ? null
                        : !finalMatches
                            ? "final_comparison_mismatch"
                            : "trace_mismatch",
                    expectedRanges,
                    actualRanges,
                    (expectedFinal ?? [])
                        .Select(comparison => comparison.ToFinalComparison())
                        .ToList(),
                    (actualFinal ?? [])
                        .Select(ToFinalComparison)
                        .ToList(),
                    expectedTrace,
                    actualTrace is null ? null : ToExpectedTraceEntry(actualTrace));
            })
            .ToList();
    }

    private static bool ExpectedFinalMatches(
        IReadOnlyList<EvaluationExpectedFinalComparison> expected,
        IReadOnlyList<TextComparison> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        var orderedExpected = expected
            .OrderBy(comparison => comparison.OriginalTextRange.InitialIndex)
            .ThenBy(comparison => comparison.UserTextRange.InitialIndex)
            .ToList();
        var orderedActual = actual
            .OrderBy(comparison => comparison.OriginalTextRange.InitialIndex)
            .ThenBy(comparison => comparison.UserTextRange.InitialIndex)
            .ToList();

        for (var index = 0; index < orderedExpected.Count; index++)
        {
            var expectedComparison = orderedExpected[index];
            var actualComparison = orderedActual[index];

            if (expectedComparison.SourceComparisonIndex
                    != actualComparison.SourceComparisonIndex
                || expectedComparison.OriginalTextRange.ToDomain()
                    != actualComparison.OriginalTextRange
                || expectedComparison.OriginalText != actualComparison.OriginalText
                || expectedComparison.UserTextRange.ToDomain()
                    != actualComparison.UserTextRange
                || expectedComparison.UserText != actualComparison.UserText
                || expectedComparison.IsDeterministicallyRefined
                    != actualComparison.IsDeterministicallyRefined)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ExpectedTraceMatches(
        EvaluationExpectedTraceEntry? expected,
        CorrectionTraceEntry? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }

        return expected.SourceComparisonIndex == actual.SourceComparisonIndex
            && SnapshotMatches(expected.Initial, actual.Initial)
            && StageMatches(expected.Deterministic, actual.Deterministic);
    }

    private static bool StageMatches(
        EvaluationExpectedStageTrace? expected,
        CorrectionStageTrace? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }

        return expected.Action == actual.Action
            && (expected.ReasonCode is null
                || expected.ReasonCode == actual.ReasonCode)
            && expected.ValidationStatus == actual.ValidationStatus
            && expected.ValidationFailureReason
                == actual.ValidationFailureReason
            && SnapshotsMatch(expected.Output, actual.Output)
            && SnapshotsMatchNullable(
                expected.ProposedOutput,
                actual.ProposedOutput);
    }

    private static bool SnapshotsMatchNullable(
        IReadOnlyList<EvaluationComparisonSnapshot>? expected,
        IReadOnlyList<ComparisonSnapshot>? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null;
        }

        return SnapshotsMatch(expected, actual);
    }

    private static bool SnapshotsMatch(
        IReadOnlyList<EvaluationComparisonSnapshot> expected,
        IReadOnlyList<ComparisonSnapshot> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (!SnapshotMatches(expected[index], actual[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SnapshotMatches(
        EvaluationComparisonSnapshot expected,
        ComparisonSnapshot actual) =>
        expected.OriginalTextRange.ToDomain() == actual.OriginalTextRange
        && expected.OriginalText == actual.OriginalText
        && expected.UserTextRange.ToDomain() == actual.UserTextRange
        && expected.UserText == actual.UserText;

    private static EvaluationFinalComparison ToFinalComparison(
        TextComparison comparison) =>
        new()
        {
            SourceComparisonIndex = comparison.SourceComparisonIndex,
            OriginalTextRange = ToEvaluationRange(comparison.OriginalTextRange),
            OriginalText = comparison.OriginalText ?? string.Empty,
            UserTextRange = ToEvaluationRange(comparison.UserTextRange),
            UserText = comparison.UserText ?? string.Empty,
            IsDeterministicallyRefined = comparison.IsDeterministicallyRefined
        };

    private static EvaluationExpectedTraceEntry ToExpectedTraceEntry(
        CorrectionTraceEntry trace) =>
        new()
        {
            SourceComparisonIndex = trace.SourceComparisonIndex,
            Initial = ToEvaluationSnapshot(trace.Initial),
            Deterministic = ToEvaluationStage(trace.Deterministic)
        };

    private static EvaluationExpectedStageTrace? ToEvaluationStage(
        CorrectionStageTrace? trace) =>
        trace is null
            ? null
            : new EvaluationExpectedStageTrace
            {
                Action = trace.Action,
                ReasonCode = trace.ReasonCode,
                Output = trace.Output.Select(ToEvaluationSnapshot).ToList(),
                ValidationStatus = trace.ValidationStatus,
                ProposedOutput = trace.ProposedOutput?
                    .Select(ToEvaluationSnapshot)
                    .ToList(),
                ValidationFailureReason = trace.ValidationFailureReason
            };

    private static EvaluationComparisonSnapshot ToEvaluationSnapshot(
        ComparisonSnapshot snapshot) =>
        new()
        {
            OriginalTextRange = ToEvaluationRange(snapshot.OriginalTextRange),
            OriginalText = snapshot.OriginalText,
            UserTextRange = ToEvaluationRange(snapshot.UserTextRange),
            UserText = snapshot.UserText
        };

    private static EvaluationTextRange ToEvaluationRange(TextRange range) =>
        new(range.InitialIndex, range.FinalIndex);

    private static string SummarizeActions(IEnumerable<string> actions)
    {
        var values = actions.Distinct(StringComparer.Ordinal).ToList();
        return values.Count == 1 ? values[0] : "mixed";
    }

    private static string DetermineAction(
        IReadOnlyList<CorrectionComparisonRange> ranges,
        EvaluationSourceComparison source)
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

    private static CorrectionComparisonRange ToRange(TextComparison comparison) =>
        new(
            comparison.SourceComparisonIndex,
            comparison.OriginalTextRange.InitialIndex,
            comparison.OriginalTextRange.FinalIndex,
            comparison.UserTextRange.InitialIndex,
            comparison.UserTextRange.FinalIndex);

    private static double CalculateSpanF1(
        IReadOnlyList<CorrectionComparisonRange> expected,
        IReadOnlyList<CorrectionComparisonRange> actual)
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

    private static IEnumerable<int> ToOriginalIndexes(CorrectionComparisonRange range) =>
        Enumerable.Range(
            range.OriginalTextInitialIndex,
            range.OriginalTextFinalIndex - range.OriginalTextInitialIndex + 1);

    private static IEnumerable<int> ToUserIndexes(CorrectionComparisonRange range) =>
        Enumerable.Range(
            range.UserTextInitialIndex,
            range.UserTextFinalIndex - range.UserTextInitialIndex + 1);
}

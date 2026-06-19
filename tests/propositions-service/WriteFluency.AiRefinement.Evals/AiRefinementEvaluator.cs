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
        CancellationToken cancellationToken)
    {
        var sourceComparison = evaluationCase.SourceComparison.ToDomain();

        if (!TrySlice(
                evaluationCase.OriginalText,
                sourceComparison.OriginalTextRange,
                out var originalSnippet)
            || !TrySlice(
                evaluationCase.UserText,
                sourceComparison.UserTextRange,
                out var userSnippet))
        {
            return Failure(evaluationCase, "Source comparison ranges cannot be sliced safely.");
        }

        if (originalSnippet != sourceComparison.OriginalText
            || userSnippet != sourceComparison.UserText)
        {
            return Failure(evaluationCase, "Source comparison snippets do not match their ranges.");
        }

        var request = new AiRefinementRequest(
            evaluationCase.OriginalText,
            evaluationCase.UserText,
            [sourceComparison]);

        try
        {
            var refinement = await _refiner.RefineAsync(request, cancellationToken);
            var validation = _validator.Validate(request, refinement.Comparisons);
            var isFullyValid = validation.IsValid
                && validation.RejectedSourceComparisonCount == 0;
            var actualRanges = (isFullyValid
                    ? validation.NormalizedRanges
                    : refinement.Comparisons)
                .OrderBy(range => range.OriginalTextInitialIndex)
                .ThenBy(range => range.UserTextInitialIndex)
                .ToList();

            var expectedRanges = evaluationCase.ExpectedRanges
                .OrderBy(range => range.OriginalTextInitialIndex)
                .ThenBy(range => range.UserTextInitialIndex)
                .ToList();

            return new EvaluationCaseResult(
                evaluationCase.CaseId,
                evaluationCase.Category,
                evaluationCase.ExpectedAction,
                isFullyValid
                    ? DetermineAction(actualRanges, sourceComparison)
                    : "error",
                isFullyValid,
                isFullyValid && expectedRanges.SequenceEqual(actualRanges),
                isFullyValid ? CalculateSpanF1(expectedRanges, actualRanges) : 0,
                refinement.DurationMilliseconds,
                refinement.InputTokenCount,
                refinement.OutputTokenCount,
                validation.FailureReason,
                expectedRanges,
                actualRanges);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(evaluationCase, exception.GetType().Name);
        }
    }

    private EvaluationSummary CreateSummary(
        IReadOnlyList<EvaluationCaseResult> results)
    {
        var expectedRemovals = results.Count(result => result.ExpectedAction == "remove");
        var actualRemovals = results.Count(result => result.ActualAction == "remove");
        var correctRemovals = results.Count(result =>
            result.ExpectedAction == "remove"
            && result.ActualAction == "remove");
        var genuineErrorRemovals = results.Count(result =>
            result.ExpectedAction != "remove"
            && result.ActualAction == "remove");
        var exactPassCount = results.Count(result => result.IsExactMatch);
        var invalidOutputCount = results.Count(result => !result.IsSafe && result.Error is not null);
        var modelFailureCount = results.Count(result =>
            !result.IsSafe
            && result.Error is not null
            && result.Error != "missing_comparisons"
            && result.Error != "unknown_source_comparison"
            && result.Error != "invalid_range"
            && result.Error != "range_outside_source"
            && result.Error != "partial_word_range"
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
            results.Count,
            exactPassCount,
            exactPassRate,
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
        string error) =>
        new(
            evaluationCase.CaseId,
            evaluationCase.Category,
            evaluationCase.ExpectedAction,
            "error",
            IsSafe: false,
            IsExactMatch: false,
            SpanF1: 0,
            DurationMilliseconds: 0,
            InputTokenCount: null,
            OutputTokenCount: null,
            Error: error,
            evaluationCase.ExpectedRanges,
            ActualRanges: []);

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

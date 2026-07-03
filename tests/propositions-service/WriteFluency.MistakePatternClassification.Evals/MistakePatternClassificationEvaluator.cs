using System.Diagnostics;
using WriteFluency.Infrastructure.TextComparisons;
using WriteFluency.TextComparisons;

namespace WriteFluency.MistakePatternClassification.Evals;

public sealed class MistakePatternClassificationEvaluator
{
    private readonly OpenAiMistakePatternClassifier _classifier;
    private readonly MistakePatternPhraseSimilarityGrader _phraseGrader;
    private readonly string _model;
    private readonly float _temperature;
    private readonly int _maxComparisonsPerRequest;
    private readonly EvaluationPricing? _pricing;

    public MistakePatternClassificationEvaluator(
        OpenAiMistakePatternClassifier classifier,
        MistakePatternPhraseSimilarityGrader phraseGrader,
        string model,
        float temperature,
        int maxComparisonsPerRequest,
        EvaluationPricing? pricing)
    {
        _classifier = classifier;
        _phraseGrader = phraseGrader;
        _model = model;
        _temperature = temperature;
        _maxComparisonsPerRequest = maxComparisonsPerRequest;
        _pricing = pricing;
    }

    public async Task<EvaluationRunSummary> EvaluateAsync(
        IReadOnlyList<EvaluationCase> cases,
        int runs,
        int concurrency,
        CancellationToken cancellationToken)
    {
        var work = Enumerable.Range(1, runs)
            .SelectMany(runNumber => cases.Select(evaluationCase => (evaluationCase, runNumber)))
            .ToArray();
        var semaphore = new SemaphoreSlim(Math.Max(1, concurrency));
        var results = new List<EvaluationCaseRunResult>();
        var tasks = work.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await EvaluateCaseAsync(
                    item.evaluationCase,
                    item.runNumber,
                    cancellationToken);
                lock (results)
                {
                    results.Add(result);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return new EvaluationRunSummary(
            _model,
            _temperature,
            _maxComparisonsPerRequest,
            _pricing,
            DateTimeOffset.UtcNow,
            results
                .OrderBy(result => result.RunNumber)
                .ThenBy(result => result.CaseId)
                .ToArray());
    }

    private async Task<EvaluationCaseRunResult> EvaluateCaseAsync(
        EvaluationCase evaluationCase,
        int runNumber,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var comparisons = evaluationCase.Comparisons
                .OrderBy(comparison => comparison.ComparisonIndex)
                .ToArray();
            var domainComparisons = comparisons
                .Select(comparison => comparison.ToDomain())
                .ToArray();
            var classificationRun = await _classifier.ClassifyWithDiagnosticsAsync(
                new MistakePatternClassificationRequest(
                    evaluationCase.OriginalText,
                    evaluationCase.UserText,
                    domainComparisons),
                cancellationToken);
            var annotationsByComparison = classificationRun.Annotations
                .GroupBy(annotation => annotation.ComparisonIndex)
                .ToDictionary(group => group.Key, group => group.First());
            var comparisonResults = comparisons
                .Select(comparison =>
                {
                    annotationsByComparison.TryGetValue(
                        comparison.ComparisonIndex,
                        out var annotation);
                    return MistakePatternClassificationScorer.Score(
                        evaluationCase,
                        comparison,
                        annotation);
                })
                .ToArray();
            var phraseGradingRun = await _phraseGrader.GradeAsync(
                evaluationCase,
                comparisonResults,
                cancellationToken);
            var phraseGradesByComparison = phraseGradingRun.Grades
                .ToDictionary(grade => grade.ComparisonIndex);
            comparisonResults = comparisonResults
                .Select(comparison =>
                    phraseGradesByComparison.TryGetValue(
                        comparison.ComparisonIndex,
                        out var phraseGrade)
                        ? ApplyPhraseGrade(comparison, phraseGrade)
                        : ApplyMissingPhraseGrade(comparison))
                .ToArray();

            stopwatch.Stop();
            return new EvaluationCaseRunResult(
                evaluationCase.CaseId,
                evaluationCase.Category,
                runNumber,
                comparisonResults.All(comparison => comparison.Passed),
                stopwatch.ElapsedMilliseconds,
                MapRequests(classificationRun.Requests)
                    .Concat(phraseGradingRun.Requests)
                    .ToArray(),
                Error: null,
                comparisonResults);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            return new EvaluationCaseRunResult(
                evaluationCase.CaseId,
                evaluationCase.Category,
                runNumber,
                Passed: false,
                stopwatch.ElapsedMilliseconds,
                Requests: [],
                exception.Message,
                evaluationCase.Comparisons
                    .Select(comparison =>
                        MistakePatternClassificationScorer.Score(
                            evaluationCase,
                            comparison,
                            actual: null))
                    .ToArray());
        }
    }

    private static IReadOnlyList<EvaluationRequestResult> MapRequests(
        IReadOnlyList<MistakePatternClassificationRequestMetrics> requests) =>
        requests
            .Select(request => new EvaluationRequestResult(
                "classifier",
                request.BatchNumber,
                request.StartIndex,
                request.ComparisonCount,
                request.DurationMilliseconds,
                request.InputTokenCount,
                request.OutputTokenCount,
                request.TotalTokenCount))
            .ToArray();

    private static EvaluationComparisonResult ApplyPhraseGrade(
        EvaluationComparisonResult comparison,
        EvaluationPhraseSimilarityGrade phraseGrade)
    {
        var failures = comparison.Failures
            .Where(failure => !failure.StartsWith(
                "phrase_similarity_below_threshold:",
                StringComparison.Ordinal))
            .ToList();
        if (!phraseGrade.Passed)
        {
            failures.Add(
                $"phrase_ai_similarity_below_threshold:score={phraseGrade.Score:0.000},reason={phraseGrade.Reason}");
        }

        var phrasePassed = phraseGrade.Passed
                           && !failures.Any(failure =>
                               failure.StartsWith("missing_phrase", StringComparison.Ordinal)
                               || failure.StartsWith("phrase_too_long", StringComparison.Ordinal)
                               || failure.StartsWith("phrase_restates_diff", StringComparison.Ordinal)
                               || failure.StartsWith("forbidden_phrase:", StringComparison.Ordinal)
                               || failure.StartsWith("multiple_sentences", StringComparison.Ordinal));

        return comparison with
        {
            PhraseAiSimilarityScore = phraseGrade.Score,
            PhraseAiSimilarityReason = phraseGrade.Reason,
            PhrasePassed = phrasePassed,
            Passed = comparison.TagsPassed && phrasePassed,
            Failures = failures
        };
    }

    private static EvaluationComparisonResult ApplyMissingPhraseGrade(
        EvaluationComparisonResult comparison)
    {
        var failures = comparison.Failures.ToList();
        failures.Add("missing_phrase_ai_grade");

        return comparison with
        {
            PhraseAiSimilarityScore = null,
            PhraseAiSimilarityReason = "The phrase grader did not return a score for this comparison.",
            PhrasePassed = false,
            Passed = false,
            Failures = failures
        };
    }
}

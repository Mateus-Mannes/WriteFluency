using System.Text.Json;
using WriteFluency.AiRefinement.Evals;
using WriteFluency.TextComparisons;

var arguments = EvaluationArguments.Parse(args);
var manifestPath = Path.Combine(
    AppContext.BaseDirectory,
    "orchestration-eval-cases.json");
var cases = JsonSerializer.Deserialize(
    await File.ReadAllTextAsync(manifestPath),
    EvaluationJsonContext.Default.ListEvaluationCase)
    ?? throw new InvalidOperationException("The evaluation manifest is empty.");

if (!string.IsNullOrWhiteSpace(arguments.CaseId))
{
    cases = cases
        .Where(item => item.CaseId == arguments.CaseId)
        .ToList();
    if (cases.Count == 0)
    {
        throw new InvalidOperationException(
            $"Evaluation case '{arguments.CaseId}' was not found.");
    }
}

EvaluationFixtureValidator.Validate(cases);
if (arguments.ValidateOnly)
{
    Console.WriteLine($"Validated {cases.Count} evaluation case(s).");
    return 0;
}

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var evaluator = new AiRefinementEvaluator(
        new CorrectionOrchestrationService(
            CreateTextComparisonService(),
            new DeterministicTextComparisonRefiner(
                new DeterministicTextEquivalenceService(
                    new EnglishNumberNormalizer()))));
    var summary = await evaluator.EvaluateAsync(
        cases,
        arguments.Runs,
        arguments.Concurrency,
        cancellation.Token);
    var reportPaths = await EvaluationReportWriter.WriteAsync(
        summary,
        cases,
        cancellation.Token);

    Console.WriteLine();
    Console.WriteLine($"Passed: {summary.Passed}");
    Console.WriteLine(
        $"Exact case runs: {summary.ExactPassCount}/{summary.CaseCount}");
    Console.WriteLine(
        $"Exact comparisons: {summary.ExactComparisonCount}/{summary.ComparisonCount}");
    Console.WriteLine(
        $"Exact focus comparisons: {summary.ExactFocusComparisonCount}/{summary.FocusComparisonCount}");
    Console.WriteLine($"Removal precision: {summary.EquivalentRemovalPrecision:P1}");
    Console.WriteLine($"Removal recall: {summary.EquivalentRemovalRecall:P1}");
    Console.WriteLine($"Mean comparison span F1: {summary.MeanComparisonSpanF1:F3}");
    Console.WriteLine($"HTML report: {reportPaths.Html}");
    Console.WriteLine($"Markdown report: {reportPaths.Markdown}");
    Console.WriteLine($"Highlights: {reportPaths.Highlights}");

    return summary.Passed || arguments.ReportOnly ? 0 : 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Evaluation canceled.");
    return 130;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

static TextComparisonService CreateTextComparisonService()
{
    var levenshteinDistanceService = new LevenshteinDistanceService();
    return new TextComparisonService(
        levenshteinDistanceService,
        new TextAlignmentService(
            new NeedlemanWunschAlignmentService(levenshteinDistanceService),
            new TokenizeTextService(),
            new TokenAlignmentService()),
        new TokenComparisonService());
}

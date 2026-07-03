using WriteFluency.MistakePatternClassification.Evals;

var arguments = EvaluationArguments.Parse(args);
var cases = await EvaluationRuntime.LoadCasesAsync(
    arguments.CaseId,
    CancellationToken.None);

EvaluationFixtureValidator.Validate(cases);
if (arguments.ValidateOnly)
{
    Console.WriteLine($"Validated {cases.Count} mistake-pattern evaluation case(s).");
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
    var classifier = EvaluationRuntime.CreateClassifier(
        arguments,
        out var options);
    var phraseGrader = EvaluationRuntime.CreatePhraseSimilarityGrader(options);
    var pricing = CreatePricing(arguments);
    var evaluator = new MistakePatternClassificationEvaluator(
        classifier,
        phraseGrader,
        options.Model,
        options.Temperature,
        options.MaxComparisonsPerRequest,
        pricing);
    var summary = await evaluator.EvaluateAsync(
        cases,
        arguments.Runs,
        arguments.Concurrency,
        cancellation.Token);
    var reportPath = await EvaluationReportWriter.WriteAsync(
        summary,
        cancellation.Token);

    Console.WriteLine();
    Console.WriteLine($"Passed: {summary.Passed}");
    Console.WriteLine($"Case runs: {summary.PassingCaseRunCount}/{summary.CaseRunCount}");
    Console.WriteLine($"Comparisons: {summary.PassingComparisonCount}/{summary.ComparisonCount}");
    Console.WriteLine($"Tag precision: {summary.TagPrecision:P1}");
    Console.WriteLine($"Tag recall: {summary.TagRecall:P1}");
    Console.WriteLine($"Tag F1: {summary.TagF1:F3}");
    Console.WriteLine($"Phrase pass rate: {summary.PhrasePassRate:P1}");
    Console.WriteLine($"Classifier requests: {summary.RequestCount}");
    Console.WriteLine($"Classifier request time: {summary.TotalRequestDurationMilliseconds} ms");
    Console.WriteLine($"Token usage: input {FormatNullable(summary.InputTokenCount)}, output {FormatNullable(summary.OutputTokenCount)}, total {FormatNullable(summary.TotalTokenCount)}");
    Console.WriteLine($"Estimated spend: {FormatCost(summary.EstimatedCostUsd)}{FormatPricing(summary.Pricing)}");
    Console.WriteLine($"HTML report: {reportPath}");

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

static string FormatNullable(long? value) => value?.ToString() ?? "unavailable";

static EvaluationPricing? CreatePricing(EvaluationArguments arguments)
{
    return new EvaluationPricing(
        arguments.InputUsdPerMillionTokens
        ?? EvaluationPricing.DefaultInputUsdPerMillionTokens,
        arguments.OutputUsdPerMillionTokens
        ?? EvaluationPricing.DefaultOutputUsdPerMillionTokens);
}

static string FormatCost(decimal? value) =>
    value is null ? "unavailable" : $"${value.Value:0.000000}";

static string FormatPricing(EvaluationPricing? pricing) =>
    pricing is null
        ? " (pricing unavailable)"
        : $" (input ${pricing.InputUsdPerMillionTokens:0.####}/1M, output ${pricing.OutputUsdPerMillionTokens:0.####}/1M)";

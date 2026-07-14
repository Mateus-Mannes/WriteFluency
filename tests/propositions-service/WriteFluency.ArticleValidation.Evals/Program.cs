using WriteFluency.ArticleValidation.Evals;

var arguments = EvaluationArguments.Parse(args);
var cases = await EvaluationRuntime.LoadCasesAsync(arguments.CaseId, CancellationToken.None);
if (arguments.ValidateOnly)
{
    Console.WriteLine($"Validated {cases.Count} article-validation evaluation case(s).");
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
    var client = EvaluationRuntime.CreateClient(arguments, out var options);
    var evaluator = new ArticleValidationEvaluator(
        client,
        options.ArticleValidationModel);
    var summary = await evaluator.EvaluateAsync(
        cases,
        arguments.Runs,
        cancellation.Token);
    await EvaluationReportWriter.WriteAsync(summary, cancellation.Token);

    Console.WriteLine();
    Console.WriteLine($"Passed: {summary.Passed}");
    Console.WriteLine($"Cases: {summary.PassingCaseCount}/{summary.CaseCount}");
    Console.WriteLine($"Accuracy: {summary.Accuracy:P1}");
    Console.WriteLine($"Model: {summary.Model}");
    Console.WriteLine("Failures:");
    foreach (var result in summary.Results.Where(result => !result.Passed))
    {
        Console.WriteLine($"- {result.CaseId}: expected {Format(result.ExpectedValid)}, got {Format(result.ActualValid)} ({result.Category})");
    }

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

static string Format(bool value) => value ? "valid" : "invalid";

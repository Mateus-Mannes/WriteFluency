using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriteFluency.AiRefinement.Evals;
using WriteFluency.Infrastructure.TextComparisons;
using WriteFluency.TextComparisons;

var arguments = EvaluationArguments.Parse(args);
var manifestPath = Path.Combine(
    AppContext.BaseDirectory,
    "ai-refinement-eval-cases.json");
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

var builder = Host.CreateApplicationBuilder();
builder.Configuration.AddUserSecrets<Program>(optional: true);

var apiKey = builder.Configuration["OPENAI_API_KEY"]
    ?? builder.Configuration["ExternalApis:OpenAI:Key"];
if (string.IsNullOrWhiteSpace(apiKey)
    || apiKey.StartsWith("{", StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        "OpenAI API key not found. Set OPENAI_API_KEY or ExternalApis:OpenAI:Key in user secrets.");
    return 2;
}

var options = new AiRefinementOptions
{
    Model = arguments.Model ?? "gpt-5.4-mini-2026-03-17",
    ReasoningEffort = "medium",
    MaxOutputTokens = 8000
};

builder.Services.AddSingleton<IOptions<AiRefinementOptions>>(Options.Create(options));
builder.Services.AddSingleton<IChatClient>(_ =>
    new OpenAI.OpenAIClient(apiKey)
        .GetChatClient(options.Model)
        .AsIChatClient());
builder.Services.AddSingleton<ITextComparisonAiRefiner, OpenAiTextComparisonRefiner>();
builder.Services.AddSingleton<AiRefinementOutputValidator>();
builder.Services.AddSingleton<AiRefinementEvaluator>();

using var host = builder.Build();
var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var evaluator = host.Services.GetRequiredService<AiRefinementEvaluator>();
    var summary = await evaluator.EvaluateAsync(
        cases,
        arguments.Runs,
        cancellation.Token);
    var reportPath = await EvaluationReportWriter.WriteAsync(
        summary,
        cancellation.Token);

    Console.WriteLine();
    Console.WriteLine($"Passed: {summary.Passed}");
    Console.WriteLine($"Exact: {summary.ExactPassCount}/{summary.CaseCount}");
    Console.WriteLine($"Removal precision: {summary.EquivalentRemovalPrecision:P1}");
    Console.WriteLine($"Removal recall: {summary.EquivalentRemovalRecall:P1}");
    Console.WriteLine($"Mean span F1: {summary.MeanSpanF1:F3}");
    Console.WriteLine($"Report: {reportPath}");

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

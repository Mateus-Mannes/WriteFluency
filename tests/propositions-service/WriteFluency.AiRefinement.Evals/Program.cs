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
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.webapi.json"),
    optional: false,
    reloadOnChange: false);
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

if (!string.IsNullOrWhiteSpace(arguments.Model))
{
    builder.Configuration[$"{AiRefinementOptions.Section}:Model"] =
        arguments.Model;
}

builder.Services.AddOptions<AiRefinementOptions>()
    .Bind(builder.Configuration.GetSection(AiRefinementOptions.Section))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Model))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ReasoningEffort))
    .Validate(options => options.MaxOutputTokens > 0)
    .Validate(options => options.MaxComparisonsPerRequest > 0)
    .ValidateOnStart();
builder.Services.AddSingleton<IChatClient>(serviceProvider =>
    new OpenAI.OpenAIClient(apiKey)
        .GetChatClient(
            serviceProvider
                .GetRequiredService<IOptions<AiRefinementOptions>>()
                .Value
                .Model)
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

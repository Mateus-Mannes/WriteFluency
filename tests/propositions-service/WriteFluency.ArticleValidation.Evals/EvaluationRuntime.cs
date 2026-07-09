using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriteFluency.Infrastructure.ExternalApis;

namespace WriteFluency.ArticleValidation.Evals;

public static class EvaluationRuntime
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public static async Task<IReadOnlyList<EvaluationCase>> LoadCasesAsync(
        string? caseId,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "article-validation-cases.json");
        var cases = JsonSerializer.Deserialize<List<EvaluationCase>>(
            await File.ReadAllTextAsync(manifestPath, cancellationToken),
            JsonOptions)
            ?? throw new InvalidOperationException("The evaluation manifest is empty.");

        ValidateCases(cases);

        if (string.IsNullOrWhiteSpace(caseId))
        {
            return cases;
        }

        var filteredCases = cases
            .Where(item => item.CaseId == caseId)
            .ToList();
        if (filteredCases.Count == 0)
        {
            throw new InvalidOperationException($"Evaluation case '{caseId}' was not found.");
        }

        return filteredCases;
    }

    public static OpenAIClient CreateClient(EvaluationArguments arguments, out OpenAIOptions options)
    {
        var configuration = CreateConfiguration();
        options = configuration
            .GetSection(OpenAIOptions.Section)
            .Get<OpenAIOptions>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{OpenAIOptions.Section}'.");
        if (string.IsNullOrWhiteSpace(options.Key) || options.Key.StartsWith('{'))
        {
            throw new InvalidOperationException(
                "OpenAI API key is missing. Configure ExternalApis:OpenAI:Key through user secrets or environment variables.");
        }

        if (!string.IsNullOrWhiteSpace(arguments.Model))
        {
            options.ArticleValidationModel = arguments.Model;
        }

        return new OpenAIClient(
            new HttpClient { BaseAddress = new Uri(options.BaseAddress) },
            NullLogger<OpenAIClient>.Instance,
            new StaticOptionsMonitor<OpenAIOptions>(options),
            CreateChatClient(options.Key));
    }

    private static IChatClient CreateChatClient(string apiKey) =>
        new ChatClientBuilder(
                new global::OpenAI.OpenAIClient(apiKey)
                    .GetChatClient("gpt-5.1")
                    .AsIChatClient())
            .UseFunctionInvocation()
            .Build();

    private static IConfigurationRoot CreateConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.newsworker.json", optional: false)
            .AddUserSecrets<EvaluationCase>(optional: true)
            .AddEnvironmentVariables()
            .Build();

    private static void ValidateCases(IReadOnlyList<EvaluationCase> cases)
    {
        if (cases.Count == 0)
        {
            throw new InvalidOperationException("The evaluation manifest must include at least one case.");
        }

        var duplicateCaseIds = cases
            .GroupBy(item => item.CaseId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateCaseIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate case ids: {string.Join(", ", duplicateCaseIds)}");
        }

        var invalidCases = cases
            .Where(item => string.IsNullOrWhiteSpace(item.ArticleContent))
            .Select(item => item.CaseId)
            .ToList();
        if (invalidCases.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cases with empty article content: {string.Join(", ", invalidCases)}");
        }
    }

    private sealed class StaticOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = currentValue;

        public TOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
}

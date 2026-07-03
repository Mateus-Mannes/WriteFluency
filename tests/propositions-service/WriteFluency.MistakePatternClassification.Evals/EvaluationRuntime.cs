using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriteFluency.Infrastructure.ExternalApis;
using WriteFluency.Infrastructure.TextComparisons;
using WriteFluency.TextComparisons;

namespace WriteFluency.MistakePatternClassification.Evals;

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
            "examples2.json");
        var sourceCases = JsonSerializer.Deserialize<List<SourceEvaluationCase>>(
            await File.ReadAllTextAsync(manifestPath, cancellationToken),
            JsonOptions)
            ?? throw new InvalidOperationException("The evaluation manifest is empty.");
        var cases = sourceCases
            .Select(sourceCase => sourceCase.ToEvaluationCase())
            .ToList();

        if (string.IsNullOrWhiteSpace(caseId))
        {
            return cases;
        }

        var filteredCases = cases
            .Where(item => item.CaseId == caseId)
            .ToList();
        if (filteredCases.Count == 0)
        {
            throw new InvalidOperationException(
                $"Evaluation case '{caseId}' was not found.");
        }

        return filteredCases;
    }

    public static OpenAiMistakePatternClassifier CreateClassifier(
        EvaluationArguments arguments,
        out MistakePatternClassificationOptions options)
    {
        var configuration = CreateConfiguration();
        var openAiOptions = configuration
            .GetSection(OpenAIOptions.Section)
            .Get<OpenAIOptions>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{OpenAIOptions.Section}'.");
        if (string.IsNullOrWhiteSpace(openAiOptions.Key)
            || openAiOptions.Key.StartsWith('{'))
        {
            throw new InvalidOperationException(
                "OpenAI API key is missing. Configure ExternalApis:OpenAI:Key through user secrets or environment variables.");
        }

        options = configuration
            .GetSection(MistakePatternClassificationOptions.Section)
            .Get<MistakePatternClassificationOptions>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{MistakePatternClassificationOptions.Section}'.");

        if (!string.IsNullOrWhiteSpace(arguments.Model))
        {
            options.Model = arguments.Model;
        }

        if (arguments.Temperature is not null)
        {
            options.Temperature = arguments.Temperature.Value;
        }

        if (arguments.MaxComparisonsPerRequest is not null)
        {
            options.MaxComparisonsPerRequest =
                arguments.MaxComparisonsPerRequest.Value;
        }

        options.Enabled = true;

        return new OpenAiMistakePatternClassifier(
            CreateChatClient(openAiOptions.Key),
            Options.Create(options),
            NullLogger<OpenAiMistakePatternClassifier>.Instance);
    }

    public static MistakePatternPhraseSimilarityGrader CreatePhraseSimilarityGrader(
        MistakePatternClassificationOptions options)
    {
        var configuration = CreateConfiguration();
        var openAiOptions = configuration
            .GetSection(OpenAIOptions.Section)
            .Get<OpenAIOptions>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{OpenAIOptions.Section}'.");
        if (string.IsNullOrWhiteSpace(openAiOptions.Key)
            || openAiOptions.Key.StartsWith('{'))
        {
            throw new InvalidOperationException(
                "OpenAI API key is missing. Configure ExternalApis:OpenAI:Key through user secrets or environment variables.");
        }

        return new MistakePatternPhraseSimilarityGrader(
            CreateChatClient(openAiOptions.Key),
            options.Model,
            options.Temperature,
            options.MaxComparisonsPerRequest);
    }

    private static IChatClient CreateChatClient(string apiKey) =>
        new ChatClientBuilder(
                new OpenAI.OpenAIClient(apiKey)
                    .GetChatClient("gpt-5.1")
                    .AsIChatClient())
            .UseFunctionInvocation()
            .Build();

    private static IConfigurationRoot CreateConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.webapi.json", optional: false)
            .AddUserSecrets<EvaluationCase>(optional: true)
            .AddEnvironmentVariables()
            .Build();
}

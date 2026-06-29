using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WriteFluency.TextComparisons;

namespace WriteFluency.CorrectionOrchestration.Evals;

public static class EvaluationRuntime
{
    public static async Task<IReadOnlyList<EvaluationCase>> LoadCasesAsync(
        string? caseId,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "orchestration-eval-cases.json");
        var cases = JsonSerializer.Deserialize(
            await File.ReadAllTextAsync(manifestPath, cancellationToken),
            EvaluationJsonContext.Default.ListEvaluationCase)
            ?? throw new InvalidOperationException("The evaluation manifest is empty.");

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

    public static CorrectionOrchestrationEvaluator CreateEvaluator() =>
        new(
            new CorrectionOrchestrationService(
                CreateTextComparisonService(),
                new DeterministicTextComparisonRefiner(
                    new DeterministicTextEquivalenceService(
                        new EnglishNumberNormalizer())),
                new TextComparisonRefinementValidator(
                    LoadRefinementValidationOptions())));

    private static TextComparisonService CreateTextComparisonService()
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

    private static TextComparisonRefinementValidationOptions
        LoadRefinementValidationOptions()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.webapi.json", optional: false)
            .Build();

        return configuration
            .GetSection(TextComparisonRefinementValidationOptions.Section)
            .Get<TextComparisonRefinementValidationOptions>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{TextComparisonRefinementValidationOptions.Section}'.");
    }
}

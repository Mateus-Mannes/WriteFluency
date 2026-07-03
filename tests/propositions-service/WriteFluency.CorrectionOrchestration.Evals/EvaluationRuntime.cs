using System.Text.Json;
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
                new EmptyMistakePatternClassifier()));

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

    private sealed class EmptyMistakePatternClassifier : IMistakePatternClassifier
    {
        public Task<IReadOnlyList<MistakePatternAnnotation>> ClassifyAsync(
            MistakePatternClassificationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MistakePatternAnnotation>>([]);
    }
}

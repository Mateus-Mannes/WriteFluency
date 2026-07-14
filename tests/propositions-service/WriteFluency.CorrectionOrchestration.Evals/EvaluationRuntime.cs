using System.Text.Json;
using Microsoft.Extensions.Options;
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

    public static CorrectionOrchestrationEvaluator CreateEvaluator()
    {
        var usageLimiter = new AllowingAiUsageLimiter();
        return new CorrectionOrchestrationEvaluator(
            new CorrectionOrchestrationService(
                CreateTextComparisonService(),
                new DeterministicTextComparisonRefiner(
                    new DeterministicTextEquivalenceService(
                        new EnglishNumberNormalizer())),
                new EmptyMistakePatternClassifier(),
                usageLimiter,
                new ProReviewEligibilityService(
                    usageLimiter,
                    Options.Create(new ProReviewTeaserOptions()))));
    }

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
        public bool IsEnabled => true;

        public Task<MistakePatternClassificationRun> ClassifyWithDiagnosticsAsync(
            MistakePatternClassificationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MistakePatternClassificationRun([], []));
    }

    private sealed class AllowingAiUsageLimiter : IAiUsageLimiter
    {
        public Task<AiUsageReservation> TryReserveAsync(
            AiUsageReservationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(AiUsageReservation.Allowed(
                request.UserId,
                request.Feature,
                "2026-07-06",
                "2026-07"));

        public Task RecordCompletionAsync(
            AiUsageReservation reservation,
            AiUsageCompletion completion,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordFailureAsync(
            AiUsageReservation reservation,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

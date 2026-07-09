using WriteFluency.Propositions;
using OpenAIClient = WriteFluency.Infrastructure.ExternalApis.OpenAIClient;

namespace WriteFluency.ArticleValidation.Evals;

public sealed class ArticleValidationEvaluator(
    OpenAIClient openAiClient,
    string model)
{
    private readonly ArticleContentPolicyValidator _deterministicPolicyValidator = new();

    public async Task<EvaluationRunSummary> EvaluateAsync(
        IReadOnlyList<EvaluationCase> cases,
        int runs,
        CancellationToken cancellationToken)
    {
        var results = new List<EvaluationRunResult>();
        for (var run = 1; run <= runs; run++)
        {
            foreach (var evaluationCase in cases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var deterministicResult =
                    _deterministicPolicyValidator.Validate(evaluationCase.ArticleContent);
                var aiResult = await openAiClient.ValidateArticleContentAsync(
                    evaluationCase.ArticleContent,
                    cancellationToken);
                var actualValid = aiResult.IsSuccess && aiResult.Value;
                var errors = aiResult.Errors
                    .Select(error => error.Message)
                    .Concat(deterministicResult.Errors.Select(error => $"deterministic: {error.Message}"))
                    .ToList();

                results.Add(new EvaluationRunResult(
                    runs == 1 ? evaluationCase.CaseId : $"{evaluationCase.CaseId}#run-{run}",
                    evaluationCase.Category,
                    evaluationCase.ExpectedValid,
                    actualValid,
                    evaluationCase.ExpectedValid == actualValid,
                    deterministicResult.IsSuccess,
                    errors,
                    evaluationCase.Reason,
                    evaluationCase.SourceUrl));
            }
        }

        return new EvaluationRunSummary(
            model,
            DateTimeOffset.UtcNow,
            results);
    }
}

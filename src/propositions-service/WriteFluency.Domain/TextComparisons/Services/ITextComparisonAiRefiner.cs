namespace WriteFluency.TextComparisons;

public interface ITextComparisonAiRefiner
{
    string Model { get; }
    string PromptVersion { get; }

    Task<AiRefinementResult> RefineAsync(
        AiRefinementRequest request,
        CancellationToken cancellationToken);
}

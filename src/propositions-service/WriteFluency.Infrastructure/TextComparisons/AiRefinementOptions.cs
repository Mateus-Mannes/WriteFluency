namespace WriteFluency.Infrastructure.TextComparisons;

public sealed class AiRefinementOptions
{
    public const string Section = "TextComparison:AiRefinement";
    public const string ChatClientKey = "text-comparison-ai-refinement";

    public string Model { get; set; } = string.Empty;
    public string ReasoningEffort { get; set; } = string.Empty;
    public int MaxOutputTokens { get; set; }
    public int MaxComparisonsPerRequest { get; set; }
}

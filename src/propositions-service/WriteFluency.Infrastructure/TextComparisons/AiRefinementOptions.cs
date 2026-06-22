namespace WriteFluency.Infrastructure.TextComparisons;

public sealed class AiRefinementOptions
{
    public const string Section = "TextComparison:AiRefinement";
    public const string ChatClientKey = "text-comparison-ai-refinement";

    public string Model { get; set; } = "gpt-5.4-nano-2026-03-17";
    public string ReasoningEffort { get; set; } = "medium";
    public int MaxOutputTokens { get; set; } = 8000;
    public int MaxComparisonsPerRequest { get; set; } = 4;
}

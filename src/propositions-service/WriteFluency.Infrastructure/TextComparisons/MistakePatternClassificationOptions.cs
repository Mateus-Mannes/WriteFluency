using System.ComponentModel.DataAnnotations;

namespace WriteFluency.Infrastructure.TextComparisons;

public sealed class MistakePatternClassificationOptions
{
    public const string Section = "TextComparison:MistakePatternClassification";

    public bool Enabled { get; set; }

    [Required]
    public required string Model { get; set; }

    [Range(100, 8000)]
    public int MaxOutputTokens { get; set; }

    [Range(1, 200)]
    public int MaxComparisonsPerRequest { get; set; }

    [Range(0, 2)]
    public float Temperature { get; set; }

    [Required]
    public required string ReasoningEffort { get; set; }
}

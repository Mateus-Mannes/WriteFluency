using System.ComponentModel.DataAnnotations;

namespace WriteFluency.TextComparisons;

public sealed class TextComparisonRefinementValidationOptions
{
    public const string Section = "TextComparison:RefinementValidation";

    [Range(1, int.MaxValue, ErrorMessage = "MaxUserTextLength must be greater than 0.")]
    public int MaxUserTextLength { get; set; }
}

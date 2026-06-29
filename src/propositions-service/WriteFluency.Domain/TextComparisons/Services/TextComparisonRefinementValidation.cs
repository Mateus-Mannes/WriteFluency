using System.ComponentModel.DataAnnotations;

namespace WriteFluency.TextComparisons;

public sealed class TextComparisonRefinementValidationOptions
{
    public const string Section = "TextComparison:RefinementValidation";

    [Range(1, int.MaxValue, ErrorMessage = "MaxUserTextLength must be greater than 0.")]
    public int MaxUserTextLength { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = "MaxUserToOriginalLengthRatio must be a non-negative number.")]
    public double MaxUserToOriginalLengthRatio { get; set; } 

    [Range(1, int.MaxValue, ErrorMessage = "MaxComparisonCount must be greater than 0.")]
    public int MaxComparisonCount { get; set; } 

    [Range(1, int.MaxValue, ErrorMessage = "MaxTotalComparisonCharacters must be greater than 0.")]
    public int MaxTotalComparisonCharacters { get; set; } 

    [Range(0.01, 1, ErrorMessage = "MaxOriginalCoverageRatio must be between 0.01 and 1.")]
    public double MaxOriginalCoverageRatio { get; set; } 

    [Range(0.01, 1, ErrorMessage = "MinStaticAccuracyPercentage must be between 0.01 and 1.")]
    public double MinStaticAccuracyPercentage { get; set; }
}

public static class TextComparisonRefinementValidationReasons
{
    public const string Valid = "valid";
    public const string SkipEmptyComparisons = "skip_empty_comparisons";
    public const string SkipTooManyComparisons = "skip_too_many_comparisons";
    public const string SkipTooManyComparisonCharacters = "skip_too_many_comparison_characters";
    public const string SkipUnstableDiff = "skip_unstable_diff";
    public const string InvalidStaticRanges = "invalid_static_ranges";
    public const string InvalidFinalRanges = "invalid_final_ranges";
}

public sealed record TextComparisonRefinementValidationResult(
    bool IsValid,
    bool ShouldSkipRefinement,
    string ReasonCode,
    IReadOnlyList<TextComparison> Comparisons)
{
    public static TextComparisonRefinementValidationResult Valid(
        IReadOnlyList<TextComparison> comparisons) =>
        new(true, false, TextComparisonRefinementValidationReasons.Valid, comparisons);

    public static TextComparisonRefinementValidationResult Skip(
        string reasonCode,
        IReadOnlyList<TextComparison> comparisons) =>
        new(true, true, reasonCode, comparisons);

    public static TextComparisonRefinementValidationResult Invalid(
        string reasonCode,
        IReadOnlyList<TextComparison> comparisons) =>
        new(false, false, reasonCode, comparisons);
}

public sealed class TextComparisonRefinementValidator
{
    private readonly TextComparisonRefinementValidationOptions _options;

    public TextComparisonRefinementValidator(
        TextComparisonRefinementValidationOptions options)
    {
        _options = options;
    }

    public TextComparisonRefinementValidationResult ValidateStatic(
        TextComparisonResult staticResult)
    {
        if (!HasValidStructure(staticResult))
        {
            return TextComparisonRefinementValidationResult.Invalid(
                TextComparisonRefinementValidationReasons.InvalidStaticRanges,
                CreateSafeFullRangeComparison(staticResult));
        }

        var comparisons = staticResult.Comparisons;
        if (comparisons.Count == 0)
        {
            return TextComparisonRefinementValidationResult.Skip(
                TextComparisonRefinementValidationReasons.SkipEmptyComparisons,
                comparisons);
        }

        if (comparisons.Count > _options.MaxComparisonCount)
        {
            return TextComparisonRefinementValidationResult.Skip(
                TextComparisonRefinementValidationReasons.SkipTooManyComparisons,
                comparisons);
        }

        if (GetTotalComparisonCharacters(comparisons)
            > _options.MaxTotalComparisonCharacters)
        {
            return TextComparisonRefinementValidationResult.Skip(
                TextComparisonRefinementValidationReasons
                    .SkipTooManyComparisonCharacters,
                comparisons);
        }

        if (IsUnstableDiff(staticResult))
        {
            return TextComparisonRefinementValidationResult.Skip(
                TextComparisonRefinementValidationReasons.SkipUnstableDiff,
                comparisons);
        }

        return TextComparisonRefinementValidationResult.Valid(comparisons);
    }

    public TextComparisonRefinementValidationResult ValidateFinal(
        string originalText,
        string userText,
        IReadOnlyList<TextComparison> comparisons)
    {
        var result = new TextComparisonResult(
            originalText,
            userText,
            0,
            comparisons.ToList());

        return HasValidStructure(result)
            ? TextComparisonRefinementValidationResult.Valid(comparisons)
            : TextComparisonRefinementValidationResult.Invalid(
                TextComparisonRefinementValidationReasons.InvalidFinalRanges,
                comparisons);
    }

    private bool IsUnstableDiff(TextComparisonResult result)
    {
        if (result.UserText.Length > _options.MaxUserTextLength)
        {
            return true;
        }

        if (result.OriginalText.Length > 0
            && ((double)result.UserText.Length / result.OriginalText.Length)
                > _options.MaxUserToOriginalLengthRatio)
        {
            return true;
        }

        if (result.AccuracyPercentage < _options.MinStaticAccuracyPercentage)
        {
            return true;
        }

        if (result.OriginalText.Length > 0
            && GetCoveredOriginalCharacterCount(result.Comparisons)
                / (double)result.OriginalText.Length
                > _options.MaxOriginalCoverageRatio)
        {
            return true;
        }

        return false;
    }

    private static bool HasValidStructure(TextComparisonResult result)
    {
        var previousOriginalEnd = -1;
        var previousUserEnd = -1;

        foreach (var comparison in result.Comparisons)
        {
            if (!IsValidComparison(result, comparison))
            {
                return false;
            }

            if (comparison.OriginalTextRange.InitialIndex <= previousOriginalEnd
                || comparison.UserTextRange.InitialIndex <= previousUserEnd)
            {
                return false;
            }

            previousOriginalEnd = comparison.OriginalTextRange.FinalIndex;
            previousUserEnd = comparison.UserTextRange.FinalIndex;
        }

        return true;
    }

    private static bool IsValidComparison(
        TextComparisonResult result,
        TextComparison comparison)
    {
        return IsValidRange(result.OriginalText, comparison.OriginalTextRange)
            && IsValidRange(result.UserText, comparison.UserTextRange)
            && TextRangeNavigator.Slice(
                result.OriginalText,
                comparison.OriginalTextRange) == comparison.OriginalText
            && TextRangeNavigator.Slice(
                result.UserText,
                comparison.UserTextRange) == comparison.UserText;
    }

    private static bool IsValidRange(string text, TextRange range) =>
        text.Length > 0
        && range.InitialIndex >= 0
        && range.FinalIndex >= range.InitialIndex
        && range.FinalIndex < text.Length;

    private static List<TextComparison> CreateSafeFullRangeComparison(
        TextComparisonResult result)
    {
        if (result.OriginalText.Length == 0 || result.UserText.Length == 0)
        {
            return [];
        }

        return
        [
            new TextComparison(
                new TextRange(0, result.OriginalText.Length - 1),
                result.OriginalText,
                new TextRange(0, result.UserText.Length - 1),
                result.UserText,
                sourceComparisonIndex: 0)
        ];
    }

    private static int GetTotalComparisonCharacters(
        IReadOnlyList<TextComparison> comparisons) =>
        comparisons.Sum(comparison =>
            (comparison.OriginalText?.Length ?? 0)
            + (comparison.UserText?.Length ?? 0));

    private static int GetCoveredOriginalCharacterCount(
        IReadOnlyList<TextComparison> comparisons) =>
        comparisons.Sum(comparison =>
            comparison.OriginalTextRange.FinalIndex
            - comparison.OriginalTextRange.InitialIndex
            + 1);
}

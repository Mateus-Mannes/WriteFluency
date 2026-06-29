using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public class TextComparisonRefinementValidatorTests
{
    [Fact]
    public void ValidateStatic_WithValidOrderedNonOverlappingComparisons_ShouldPass()
    {
        var validator = CreateValidator();
        var result = CreateResult(
            "alpha beta gamma",
            "alfa beta gama",
            [
                CreateComparison(0, "alpha beta gamma", 0, 4, "alfa beta gama", 0, 3),
                CreateComparison(1, "alpha beta gamma", 11, 15, "alfa beta gama", 10, 13)
            ]);

        var validation = validator.ValidateStatic(result);

        validation.IsValid.ShouldBeTrue();
        validation.ShouldSkipRefinement.ShouldBeFalse();
        validation.ReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.Valid);
    }

    [Theory]
    [InlineData(-1, 1, 0, 1)]
    [InlineData(0, 99, 0, 1)]
    [InlineData(3, 1, 0, 1)]
    [InlineData(0, 1, -1, 1)]
    [InlineData(0, 1, 0, 99)]
    [InlineData(0, 1, 3, 1)]
    public void ValidateStatic_WithInvalidRange_ShouldReturnSafeStaticFallback(
        int originalStart,
        int originalEnd,
        int userStart,
        int userEnd)
    {
        var validator = CreateValidator();
        var result = CreateResult(
            "abcd",
            "abxd",
            [
                new TextComparison(
                    new TextRange(originalStart, originalEnd),
                    "ab",
                    new TextRange(userStart, userEnd),
                    "ab")
            ]);

        var validation = validator.ValidateStatic(result);

        validation.IsValid.ShouldBeFalse();
        validation.ReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.InvalidStaticRanges);
        validation.Comparisons.Single().OriginalText.ShouldBe("abcd");
        validation.Comparisons.Single().UserText.ShouldBe("abxd");
    }

    [Fact]
    public void ValidateStatic_WithStaleSnippet_ShouldReturnSafeStaticFallback()
    {
        var validator = CreateValidator();
        var result = CreateResult(
            "alpha",
            "alfa",
            [
                new TextComparison(
                    new TextRange(0, 4),
                    "wrong",
                    new TextRange(0, 3),
                    "alfa")
            ]);

        var validation = validator.ValidateStatic(result);

        validation.IsValid.ShouldBeFalse();
        validation.ReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.InvalidStaticRanges);
    }

    [Fact]
    public void ValidateStatic_WithOverlappingOriginalRanges_ShouldReturnSafeStaticFallback()
    {
        var validator = CreateValidator();
        var original = "alpha beta gamma";
        var user = "alfa beta gama";
        var result = CreateResult(
            original,
            user,
            [
                CreateComparison(0, original, 0, 6, user, 0, 4),
                CreateComparison(1, original, 5, 9, user, 6, 9)
            ]);

        var validation = validator.ValidateStatic(result);

        validation.IsValid.ShouldBeFalse();
        validation.ReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.InvalidStaticRanges);
    }

    [Fact]
    public void ValidateStatic_WithNonMonotonicUserRanges_ShouldReturnSafeStaticFallback()
    {
        var validator = CreateValidator();
        var original = "alpha beta gamma";
        var user = "alfa beta gama";
        var result = CreateResult(
            original,
            user,
            [
                CreateComparison(0, original, 0, 4, user, 10, 13),
                CreateComparison(1, original, 11, 15, user, 0, 3)
            ]);

        var validation = validator.ValidateStatic(result);

        validation.IsValid.ShouldBeFalse();
        validation.ReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.InvalidStaticRanges);
    }

    [Fact]
    public void ValidateStatic_WithEmptyComparisons_ShouldSkipRefinement()
    {
        var validator = CreateValidator();
        var result = CreateResult("same", "same", []);

        var validation = validator.ValidateStatic(result);

        validation.IsValid.ShouldBeTrue();
        validation.ShouldSkipRefinement.ShouldBeTrue();
        validation.ReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.SkipEmptyComparisons);
    }

    [Fact]
    public void ValidateStatic_WithTooManyComparisons_ShouldSkipRefinement()
    {
        var validator = CreateValidator(new()
        {
            MaxComparisonCount = 1
        });
        var original = "alpha beta gamma";
        var user = "alfa beta gama";
        var result = CreateResult(
            original,
            user,
            [
                CreateComparison(0, original, 0, 4, user, 0, 3),
                CreateComparison(1, original, 11, 15, user, 10, 13)
            ]);

        var validation = validator.ValidateStatic(result);

        validation.ShouldSkipRefinement.ShouldBeTrue();
        validation.ReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.SkipTooManyComparisons);
    }

    [Theory]
    [InlineData("user_length")]
    [InlineData("length_ratio")]
    [InlineData("coverage")]
    [InlineData("accuracy")]
    [InlineData("characters")]
    public void ValidateStatic_WithUnstableThresholdExceeded_ShouldSkipRefinement(
        string threshold)
    {
        var options = new TextComparisonRefinementValidationOptions();
        var original = "alpha beta gamma";
        var user = "alfa beta gama";
        List<TextComparison> comparisons =
        [
            CreateComparison(0, original, 0, 4, user, 0, 3)
        ];
        var accuracy = 0.8;

        switch (threshold)
        {
            case "user_length":
                options.MaxUserTextLength = 3;
                break;
            case "length_ratio":
                options.MaxUserToOriginalLengthRatio = 0.1;
                break;
            case "coverage":
                options.MaxOriginalCoverageRatio = 0.01;
                break;
            case "accuracy":
                options.MinStaticAccuracyPercentage = 0.99;
                break;
            case "characters":
                options.MaxTotalComparisonCharacters = 1;
                break;
        }

        var validation = CreateValidator(options)
            .ValidateStatic(CreateResult(original, user, comparisons, accuracy));

        validation.ShouldSkipRefinement.ShouldBeTrue();
    }

    [Fact]
    public void ValidateFinal_WithInvalidDeterministicOutput_ShouldFailFinalValidation()
    {
        var validator = CreateValidator();
        var validation = validator.ValidateFinal(
            "alpha",
            "alfa",
            [
                new TextComparison(
                    new TextRange(0, 4),
                    "wrong",
                    new TextRange(0, 3),
                    "alfa")
            ]);

        validation.IsValid.ShouldBeFalse();
        validation.ReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.InvalidFinalRanges);
    }

    private static TextComparisonRefinementValidator CreateValidator(
        TextComparisonRefinementValidationOptions? options = null) =>
        new(options ?? new TextComparisonRefinementValidationOptions());

    private static TextComparisonResult CreateResult(
        string originalText,
        string userText,
        List<TextComparison> comparisons,
        double accuracy = 0.8) =>
        new(originalText, userText, accuracy, comparisons);

    private static TextComparison CreateComparison(
        int sourceComparisonIndex,
        string originalText,
        int originalStart,
        int originalEnd,
        string userText,
        int userStart,
        int userEnd) =>
        new(
            new TextRange(originalStart, originalEnd),
            originalText.Substring(originalStart, originalEnd - originalStart + 1),
            new TextRange(userStart, userEnd),
            userText.Substring(userStart, userEnd - userStart + 1),
            sourceComparisonIndex);
}

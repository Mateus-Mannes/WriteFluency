using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public class AiRefinementOutputValidatorTests
{
    private readonly AiRefinementOutputValidator _validator = new();

    [Fact]
    public void Validate_WhenOutputIsEmpty_ShouldRemoveAllComparisons()
    {
        var result = _validator.Validate(CreateRequest(), []);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenRangesShrinkWithinSource_ShouldSliceAuthoritativeText()
    {
        var request = CreateRequest();

        var result = _validator.Validate(
            request,
            [
                new AiRefinedComparison(
                    SourceComparisonIndex: 0,
                    OriginalTextInitialIndex: 2,
                    OriginalTextFinalIndex: 4,
                    UserTextInitialIndex: 2,
                    UserTextFinalIndex: 4)
            ]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Single().OriginalText.ShouldBe("cat");
        result.Comparisons.Single().UserText.ShouldBe("cot");
    }

    [Fact]
    public void Validate_WhenOneSourceIsSplit_ShouldReturnOrderedComparisons()
    {
        var request = CreateRequest();

        var result = _validator.Validate(
            request,
            [
                new AiRefinedComparison(0, 12, 14, 12, 14),
                new AiRefinedComparison(0, 2, 4, 2, 4)
            ]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Count.ShouldBe(2);
        result.Comparisons[0].OriginalText.ShouldBe("cat");
        result.Comparisons[1].OriginalText.ShouldBe("dog");
    }

    [Theory]
    [InlineData(-1, 4, 2, 4, "invalid_range")]
    [InlineData(4, 2, 2, 4, "invalid_range")]
    [InlineData(0, 4, 2, 4, "range_outside_source")]
    [InlineData(2, 4, 2, 20, "invalid_range")]
    public void Validate_WhenRangeIsUnsafe_ShouldRejectEntireOutput(
        int originalStart,
        int originalEnd,
        int userStart,
        int userEnd,
        string expectedReason)
    {
        var result = _validator.Validate(
            CreateRequest(),
            [
                new AiRefinedComparison(
                    0,
                    originalStart,
                    originalEnd,
                    userStart,
                    userEnd)
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(expectedReason);
        result.Comparisons.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenSourceIndexDoesNotExist_ShouldRejectOutput()
    {
        var result = _validator.Validate(
            CreateRequest(),
            [new AiRefinedComparison(99, 2, 4, 2, 4)]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("unknown_source_comparison");
    }

    [Fact]
    public void Validate_WhenOutputIsNull_ShouldRejectOutput()
    {
        var result = _validator.Validate(CreateRequest(), null);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("missing_comparisons");
    }

    private static AiRefinementRequest CreateRequest()
    {
        const string originalText = "A cat and a dog run.";
        const string userText = "A cot and a dug run.";

        return new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(2, 14),
                    originalText[2..15],
                    new TextRange(2, 14),
                    userText[2..15])
            ]);
    }
}

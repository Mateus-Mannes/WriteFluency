using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public class AiRefinementOutputValidatorTests
{
    private readonly AiRefinementOutputValidator _validator = new();

    [Fact]
    public void ValidateDecisions_ShouldApplyKeepRemoveAndRefineIndependently()
    {
        var request = CreateRequestWithSeparateSources();

        var result = _validator.ValidateDecisions(
            request,
            [
                new AiRefinementDecision(
                    0,
                    AiRefinementActions.Remove,
                    "equivalent_transcription",
                    []),
                new AiRefinementDecision(
                    1,
                    AiRefinementActions.Refine,
                    "word_substitution",
                    [new AiRefinedComparison(1, 12, 14, 12, 14)])
            ]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Single().SourceComparisonIndex.ShouldBe(1);
        result.Comparisons.Single().IsAiRefined.ShouldBeFalse();
        result.Decisions.Count.ShouldBe(2);
        result.Decisions.Single(decision => decision.SourceComparisonIndex == 0)
            .IsEffectiveChange.ShouldBeTrue();
    }

    [Fact]
    public void ValidateDecisions_WhenDecisionIsMissing_ShouldFallbackOnlyMissingSource()
    {
        var request = CreateRequestWithSeparateSources();

        var result = _validator.ValidateDecisions(
            request,
            [
                new AiRefinementDecision(
                    0,
                    AiRefinementActions.Remove,
                    "equivalent_transcription",
                    [])
            ]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Single().SourceComparisonIndex.ShouldBe(1);
        result.RejectedSources.Single().Reason.ShouldBe("missing_source_decision");
    }

    [Fact]
    public void ValidateDecisions_WhenRefinementShrinksSource_ShouldMarkFinalComparisonAsAiRefined()
    {
        var request = CreateRequest();

        var result = _validator.ValidateDecisions(
            request,
            [
                new AiRefinementDecision(
                    0,
                    AiRefinementActions.Refine,
                    "split_genuine_differences",
                    [new AiRefinedComparison(0, 2, 4, 2, 4)])
            ]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Single().IsAiRefined.ShouldBeTrue();
        result.Decisions.Single().IsEffectiveChange.ShouldBeTrue();
    }

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

    [Fact]
    public void Validate_WhenOneSourceFails_ShouldKeepValidSourcesAndRestoreRejectedSource()
    {
        var request = CreateRequestWithSeparateSources();

        var result = _validator.Validate(
            request,
            [
                new AiRefinedComparison(0, 2, 4, 2, 4),
                new AiRefinedComparison(1, 12, 18, 12, 18)
            ]);

        result.IsValid.ShouldBeTrue();
        result.FailureReason.ShouldBe("range_outside_source");
        result.RejectedSourceComparisonCount.ShouldBe(1);
        result.RejectedSources.Single().ShouldBe(
            new AiRefinementValidationIssue(1, "range_outside_source"));
        result.Comparisons.Count.ShouldBe(2);
        result.Comparisons[0].OriginalText.ShouldBe("cat");
        result.Comparisons[0].UserText.ShouldBe("cot");
        result.Comparisons[1].OriginalText.ShouldBe("dog");
        result.Comparisons[1].UserText.ShouldBe("dug");
    }

    [Fact]
    public void Validate_WhenOneCandidateInASplitSourceFails_ShouldRestoreThatWholeSource()
    {
        var request = CreateRequestWithSeparateSources();

        var result = _validator.Validate(
            request,
            [
                new AiRefinedComparison(0, 2, 4, 2, 4),
                new AiRefinedComparison(1, 12, 14, 12, 14),
                new AiRefinedComparison(1, 12, 18, 12, 18)
            ]);

        result.IsValid.ShouldBeTrue();
        result.RejectedSourceComparisonCount.ShouldBe(1);
        result.Comparisons.Count.ShouldBe(2);
        result.Comparisons.Count(comparison => comparison.OriginalText == "dog").ShouldBe(1);
        result.NormalizedRanges.Count(range => range.SourceComparisonIndex == 1).ShouldBe(1);
        result.NormalizedRanges.Single(range => range.SourceComparisonIndex == 1).ShouldBe(
            new AiRefinedComparison(1, 12, 14, 12, 14));
    }

    [Fact]
    public void Validate_WhenRangeExceedsSourceByWhitespace_ShouldTrimToSource()
    {
        var result = _validator.Validate(
            CreateRequest(),
            [new AiRefinedComparison(0, 2, 15, 2, 15)]);

        result.IsValid.ShouldBeTrue();
        var comparison = result.Comparisons.Single();
        comparison.OriginalTextRange.ShouldBe(new TextRange(2, 14));
        comparison.UserTextRange.ShouldBe(new TextRange(2, 14));
        comparison.OriginalText.ShouldBe("cat and a dog");
        comparison.UserText.ShouldBe("cot and a dug");
    }

    [Fact]
    public void Validate_WhenRangeExceedsSourceByPunctuation_ShouldTrimToSource()
    {
        const string originalText = "A (cat), runs.";
        const string userText = "A [cot]; runs.";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(3, 5),
                    "cat",
                    new TextRange(3, 5),
                    "cot")
            ]);

        var result = _validator.Validate(
            request,
            [new AiRefinedComparison(0, 2, 7, 2, 7)]);

        result.IsValid.ShouldBeTrue();
        var comparison = result.Comparisons.Single();
        comparison.OriginalText.ShouldBe("cat");
        comparison.UserText.ShouldBe("cot");
    }

    [Theory]
    [InlineData("A catx runs.", "A cotx runs.")]
    [InlineData("A cat1 runs.", "A cot1 runs.")]
    [InlineData("A cat$ runs.", "A cot$ runs.")]
    public void Validate_WhenOverflowContainsMeaningfulCharacter_ShouldRejectEntireOutput(
        string originalText,
        string userText)
    {
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(2, 4),
                    originalText[2..5],
                    new TextRange(2, 4),
                    userText[2..5])
            ]);

        var result = _validator.Validate(
            request,
            [new AiRefinedComparison(0, 2, 5, 2, 5)]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("range_outside_source");
        result.Comparisons.ShouldBeEmpty();
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

    [Theory]
    [InlineData(3, 4, 2, 4)]
    [InlineData(2, 3, 2, 4)]
    [InlineData(2, 4, 3, 4)]
    [InlineData(2, 4, 2, 3)]
    public void Validate_WhenRangeCutsThroughWord_ShouldExpandToWordBoundaries(
        int originalStart,
        int originalEnd,
        int userStart,
        int userEnd)
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

        result.IsValid.ShouldBeTrue();
        var comparison = result.Comparisons.Single();
        comparison.OriginalText.ShouldBe("cat");
        comparison.UserText.ShouldBe("cot");
        result.NormalizedRanges.Single().ShouldBe(
            new AiRefinedComparison(0, 2, 4, 2, 4));
    }

    [Fact]
    public void Validate_WhenRangeIncludesBoundaryWhitespace_ShouldTrimWhitespace()
    {
        var result = _validator.Validate(
            CreateRequest(),
            [new AiRefinedComparison(0, 1, 5, 1, 5)]);

        result.IsValid.ShouldBeTrue();
        var comparison = result.Comparisons.Single();
        comparison.OriginalText.ShouldBe("cat");
        comparison.UserText.ShouldBe("cot");
        result.NormalizedRanges.Single().ShouldBe(
            new AiRefinedComparison(0, 2, 4, 2, 4));
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

    private static AiRefinementRequest CreateRequestWithSeparateSources()
    {
        const string originalText = "A cat and a dog run.";
        const string userText = "A cot and a dug run.";

        return new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(2, 4),
                    "cat",
                    new TextRange(2, 4),
                    "cot"),
                new AiRefinementSourceComparison(
                    1,
                    new TextRange(12, 14),
                    "dog",
                    new TextRange(12, 14),
                    "dug")
            ]);
    }
}

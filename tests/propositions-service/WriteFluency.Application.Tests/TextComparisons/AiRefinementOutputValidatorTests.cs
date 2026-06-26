using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public class AiRefinementOutputValidatorTests
{
    private readonly AiRefinementOutputValidator _validator = new();

    [Fact]
    public void ValidateDecisions_WhenDecisionsAreNull_ShouldRejectOutput()
    {
        var result = _validator.ValidateDecisions(CreateRequest(), null);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("missing_decisions");
    }

    [Fact]
    public void ValidateDecisions_WhenSourceIndexIsUnknown_ShouldRejectOutput()
    {
        var result = _validator.ValidateDecisions(
            CreateRequest(),
            [
                new AiRefinementDecision(
                    99,
                    AiRefinementActions.Keep,
                    "source_range_already_minimal",
                    [])
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("unknown_source_comparison");
    }

    [Fact]
    public void ValidateDecisions_WhenSourceHasDuplicateDecisions_ShouldRejectThatSource()
    {
        var result = _validator.ValidateDecisions(
            CreateRequest(),
            [
                new AiRefinementDecision(
                    0,
                    AiRefinementActions.Keep,
                    "source_range_already_minimal",
                    []),
                new AiRefinementDecision(
                    0,
                    AiRefinementActions.Remove,
                    "equivalent_formatting",
                    [])
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("duplicate_source_decision");
    }

    [Theory]
    [InlineData(AiRefinementActions.Keep, true, "invalid_action_ranges")]
    [InlineData(AiRefinementActions.Remove, true, "invalid_action_ranges")]
    [InlineData(AiRefinementActions.Refine, false, "invalid_action_ranges")]
    [InlineData("replace", false, "invalid_action")]
    public void ValidateDecisions_WhenActionShapeIsInvalid_ShouldRejectSource(
        string action,
        bool includeRange,
        string expectedReason)
    {
        var result = _validator.ValidateDecisions(
            CreateRequest(),
            [
                new AiRefinementDecision(
                    0,
                    action,
                    "test",
                    includeRange
                        ? [new AiRefinedComparison(0, 2, 4, 2, 4)]
                        : [])
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(expectedReason);
    }

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
    public void ValidateDecisions_WhenRefinementReturnsMultipleRanges_ShouldRejectSource()
    {
        var request = CreateRequest();

        var result = _validator.ValidateDecisions(
            request,
            [
                new AiRefinementDecision(
                    0,
                    AiRefinementActions.Refine,
                    "split_genuine_differences",
                    [
                        new AiRefinedComparison(0, 2, 4, 2, 4),
                        new AiRefinedComparison(0, 12, 14, 12, 14)
                    ])
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
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
    public void Validate_WhenOneSourceReturnsMultipleRanges_ShouldRejectSource()
    {
        var request = CreateRequest();

        var result = _validator.Validate(
            request,
            [
                new AiRefinedComparison(0, 12, 14, 12, 14),
                new AiRefinedComparison(0, 2, 4, 2, 4)
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
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
    public void Validate_WhenOneSourceReturnsMultipleRangesWithAnotherValidSource_ShouldRestoreThatWholeSource()
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
        result.RejectedSources.Single().Reason.ShouldBe("invalid_action_ranges");
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

    [Fact]
    public void Validate_WhenTrailingOverflowCutsIntoNextWord_ShouldClampToSource()
    {
        const string originalText = "sequins the";
        const string userText = "sequence the";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(0, 6),
                    "sequins",
                    new TextRange(0, 7),
                    "sequence")
            ]);

        var result = _validator.Validate(
            request,
            [new AiRefinedComparison(0, 0, 8, 0, 8)]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            new AiRefinedComparison(0, 0, 6, 0, 7));
    }

    [Fact]
    public void Validate_WhenTrailingOverflowIncludesNextWord_ShouldClampToSource()
    {
        const string originalText = "embroidery a";
        const string userText = "brodery a";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(0, 9),
                    "embroidery",
                    new TextRange(0, 6),
                    "brodery")
            ]);

        var result = _validator.Validate(
            request,
            [new AiRefinedComparison(0, 0, 10, 0, 8)]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            new AiRefinedComparison(0, 0, 9, 0, 6));
    }

    [Fact]
    public void Validate_WhenTrailingOverflowReachesTwoFollowingWords_ShouldReject()
    {
        const string originalText = "cat with two words";
        const string userText = "cot with two words";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(0, 2),
                    "cat",
                    new TextRange(0, 2),
                    "cot")
            ]);

        var result = _validator.Validate(
            request,
            [new AiRefinedComparison(0, 0, 11, 0, 11)]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("range_outside_source");
    }

    [Fact]
    public void Validate_WhenLeadingOverflowCutsIntoPreviousWord_ShouldClampToSource()
    {
        const string originalText = "the sequins";
        const string userText = "the sequence";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(4, 10),
                    "sequins",
                    new TextRange(4, 11),
                    "sequence")
            ]);

        var result = _validator.Validate(
            request,
            [new AiRefinedComparison(0, 2, 10, 3, 11)]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            new AiRefinedComparison(0, 4, 10, 4, 11));
    }

    [Fact]
    public void Validate_WhenLeadingOverflowIncludesPreviousFunctionWord_ShouldClampToSource()
    {
        const string originalText = "a embroidery";
        const string userText = "a brodery";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(2, 11),
                    "embroidery",
                    new TextRange(2, 8),
                    "brodery")
            ]);

        var result = _validator.Validate(
            request,
            [new AiRefinedComparison(0, 0, 11, 0, 8)]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            new AiRefinedComparison(0, 2, 11, 2, 8));
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
    public void Validate_WhenLeadingOverflowIncludesPreviousContentWord_ShouldRejectEntireOutput()
    {
        const string originalText = "bright sequins";
        const string userText = "bright sequence";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(7, 13),
                    "sequins",
                    new TextRange(7, 14),
                    "sequence")
            ]);

        var result = _validator.Validate(
            request,
            [new AiRefinedComparison(0, 0, 13, 0, 14)]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("range_outside_source");
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

    [Theory]
    [InlineData("sun", "SUN", 0, 2, 0, 2)]
    [InlineData(" sun ", " SUN ", 0, 4, 0, 4)]
    [InlineData("sun", "the sun", 0, 2, 4, 6)]
    public void Validate_WhenSelectedTextMatchesAfterTrimAndCase_ShouldRejectOutput(
        string originalText,
        string userText,
        int originalStart,
        int originalEnd,
        int userStart,
        int userEnd)
    {
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(0, originalText.Length - 1),
                    originalText,
                    new TextRange(0, userText.Length - 1),
                    userText)
            ]);

        var result = _validator.Validate(
            request,
            [
                new AiRefinedComparison(
                    0,
                    originalStart,
                    originalEnd,
                    userStart,
                    userEnd)
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("identical_selected_text");
        result.Comparisons.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("daughters'", "daughters")]
    [InlineData("employer\u2019s", "employers")]
    public void Validate_WhenSelectionDiffersByPossessiveApostrophe_ShouldPreserveDifference(
        string originalText,
        string userText)
    {
        var request = CreateSingleSourceRequest(originalText, userText);

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, originalText, userText)]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Single().OriginalText.ShouldBe(originalText);
        result.Comparisons.Single().UserText.ShouldBe(userText);
    }

    [Fact]
    public void ValidateDecisions_WhenOneSelectionIsIdentical_ShouldFallbackOnlyThatSource()
    {
        const string originalText = "sun cat";
        const string userText = "the sun cot";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(0, 2),
                    "sun",
                    new TextRange(0, 6),
                    "the sun"),
                new AiRefinementSourceComparison(
                    1,
                    new TextRange(4, 6),
                    "cat",
                    new TextRange(8, 10),
                    "cot")
            ]);

        var result = _validator.ValidateDecisions(
            request,
            [
                new AiRefinementDecision(
                    0,
                    AiRefinementActions.Refine,
                    "genuine_insertion_or_omission",
                    [new AiRefinedComparison(0, 0, 2, 4, 6)]),
                new AiRefinementDecision(
                    1,
                    AiRefinementActions.Refine,
                    "genuine_word_difference",
                    [new AiRefinedComparison(1, 4, 6, 8, 10)])
            ]);

        result.IsValid.ShouldBeTrue();
        result.RejectedSources.Single().ShouldBe(
            new AiRefinementValidationIssue(0, "identical_selected_text"));
        result.Decisions.Single(decision => decision.SourceComparisonIndex == 0)
            .ValidationStatus.ShouldBe("rejected");
        result.Decisions.Single(decision => decision.SourceComparisonIndex == 1)
            .ValidationStatus.ShouldBe("accepted");
        result.Comparisons.Count.ShouldBe(2);
    }

    [Fact]
    public void Validate_WhenIdenticalSelectionHidesPrecedingInsertion_ShouldExpandInsertedSide()
    {
        const string originalText = "aging without sun";
        const string userText = "ageing without the sun";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    7,
                    new TextRange(0, originalText.Length - 1),
                    originalText,
                    new TextRange(0, userText.Length - 1),
                    userText)
            ]);

        var result = _validator.Validate(
            request,
            [
                new AiRefinedComparison(
                    7,
                    originalText.LastIndexOf("sun", StringComparison.Ordinal),
                    originalText.Length - 1,
                    userText.LastIndexOf("sun", StringComparison.Ordinal),
                    userText.Length - 1)
            ]);

        result.IsValid.ShouldBeTrue();
        var comparison = result.Comparisons.Single();
        comparison.OriginalText.ShouldBe("sun");
        comparison.UserText.ShouldBe("the sun");
    }

    [Fact]
    public void Validate_WhenIdenticalSelectionHasDifferentPreviousWords_ShouldNotGuessInsertion()
    {
        const string originalText = "cat sun";
        const string userText = "dog sun";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    0,
                    new TextRange(0, originalText.Length - 1),
                    originalText,
                    new TextRange(0, userText.Length - 1),
                    userText)
            ]);

        var result = _validator.Validate(
            request,
            [
                new AiRefinedComparison(
                    0,
                    4,
                    6,
                    4,
                    6)
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("identical_selected_text");
    }

    [Fact]
    public void Validate_WhenIdenticalSelectionHidesFollowingUserInsertion_ShouldExpandUserSide()
    {
        var request = CreateSingleSourceRequest(
            "sun protection",
            "sun extra protection");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "sun", "sun")]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Single().OriginalText.ShouldBe("sun");
        result.Comparisons.Single().UserText.ShouldBe("sun extra");
    }

    [Fact]
    public void Validate_WhenIdenticalSelectionHidesPrecedingOriginalInsertion_ShouldExpandOriginalSide()
    {
        var request = CreateSingleSourceRequest(
            "without the sun",
            "without sun");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "sun", "sun")]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Single().OriginalText.ShouldBe("the sun");
        result.Comparisons.Single().UserText.ShouldBe("sun");
    }

    [Fact]
    public void Validate_WhenIdenticalSelectionHidesFollowingOriginalInsertion_ShouldExpandOriginalSide()
    {
        var request = CreateSingleSourceRequest(
            "sun extra protection",
            "sun protection");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "sun", "sun")]);

        result.IsValid.ShouldBeTrue();
        result.Comparisons.Single().OriginalText.ShouldBe("sun extra");
        result.Comparisons.Single().UserText.ShouldBe("sun");
    }

    [Fact]
    public void Validate_WhenIdenticalSelectionHidesMultipleInsertedWords_ShouldNotGuess()
    {
        var request = CreateSingleSourceRequest(
            "without sun",
            "without very bright sun");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "sun", "sun")]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("identical_selected_text");
    }

    [Fact]
    public void Validate_WhenIdenticalSelectionHasNoSharedAnchor_ShouldNotGuess()
    {
        var request = CreateSingleSourceRequest("sun", "the sun");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "sun", "sun")]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("identical_selected_text");
    }

    [Fact]
    public void Validate_WhenInsertionRepairUsesAbsoluteOffsets_ShouldReturnAbsoluteRanges()
    {
        const string originalText = "prefix aging without sun suffix";
        const string userText = "prefix ageing without the sun suffix";
        var originalSourceStart = originalText.IndexOf(
            "aging",
            StringComparison.Ordinal);
        var originalSourceEnd = originalText.IndexOf(
            " suffix",
            StringComparison.Ordinal) - 1;
        var userSourceStart = userText.IndexOf(
            "ageing",
            StringComparison.Ordinal);
        var userSourceEnd = userText.IndexOf(
            " suffix",
            StringComparison.Ordinal) - 1;
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    7,
                    new TextRange(originalSourceStart, originalSourceEnd),
                    originalText[originalSourceStart..(originalSourceEnd + 1)],
                    new TextRange(userSourceStart, userSourceEnd),
                    userText[userSourceStart..(userSourceEnd + 1)])
            ]);

        var result = _validator.Validate(
            request,
            [CreateRange(request, 7, "sun", "sun")]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(request, 7, "sun", "the sun"));
    }

    [Fact]
    public void ValidateDecisions_WhenInsertionIsRepaired_ShouldPreserveProposalAndMarkEffectiveOutput()
    {
        var request = CreateSingleSourceRequest(
            "aging without sun",
            "ageing without the sun",
            sourceComparisonIndex: 7);
        var proposed = CreateRange(request, 7, "sun", "sun");

        var result = _validator.ValidateDecisions(
            request,
            [
                new AiRefinementDecision(
                    7,
                    AiRefinementActions.Refine,
                    "genuine_insertion_or_omission",
                    [proposed])
            ]);

        result.IsValid.ShouldBeTrue();
        var decision = result.Decisions.Single();
        decision.ValidationStatus.ShouldBe("accepted");
        decision.IsEffectiveChange.ShouldBeTrue();
        decision.ProposedRanges.Single().ShouldBe(proposed);
        decision.OutputComparisons.Single().OriginalText.ShouldBe("sun");
        decision.OutputComparisons.Single().UserText.ShouldBe("the sun");
    }

    [Fact]
    public void Validate_WhenRangeIncludesMatchingTrailingWord_ShouldTrimIt()
    {
        var request = CreateRetirementRequest();

        var result = _validator.Validate(
            request,
            [
                CreateRange(
                    request,
                    "advantage of their",
                    "advantages their")
            ]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(request, "advantage of", "advantages"));
    }

    [Fact]
    public void Validate_WhenRangeIncludesMatchingLeadingWord_ShouldTrimIt()
    {
        var request = CreateSingleSourceRequest(
            "their advantage of",
            "their advantages");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "their advantage of", "their advantages")]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(request, 0, "advantage of", "advantages"));
    }

    [Fact]
    public void Validate_WhenRangeIncludesMultipleMatchingBoundaryWords_ShouldTrimBothSides()
    {
        var request = CreateSingleSourceRequest(
            "the advantage of their",
            "the advantages their");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "the advantage of their", "the advantages their")]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(request, 0, "advantage of", "advantages"));
    }

    [Fact]
    public void Validate_WhenAdjacentErrorsAreReturnedAsMultipleRanges_ShouldRejectSource()
    {
        var request = CreateRetirementRequest();

        var result = _validator.Validate(
            request,
            [
                CreateRange(request, "advantage", "advantages"),
                CreateRange(request, "of their", "their")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void Validate_WhenThreeAdjacentErrorsAreReturnedAsMultipleRanges_ShouldRejectSource()
    {
        var request = CreateSingleSourceRequest(
            "bad old road",
            "sad new roads");

        var result = _validator.Validate(
            request,
            [
                CreateRange(request, 0, "bad", "sad"),
                CreateRange(request, 0, "old", "new"),
                CreateRange(request, 0, "road", "roads")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void Validate_WhenErrorsAreSeparatedByMatchingWordButReturnedAsMultipleRanges_ShouldRejectSource()
    {
        var request = CreateSingleSourceRequest(
            "cat and dog",
            "cot and dug");

        var result = _validator.Validate(
            request,
            [
                CreateRange(request, 0, "cat", "cot"),
                CreateRange(request, 0, "dog", "dug")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void Validate_WhenBroadRangeHasReliableFunctionWordAnchor_ShouldKeepSingleRange()
    {
        var request = CreateSingleSourceRequest(
            "her heritage from Hawaii",
            "a hair teacher from a while");

        var result = _validator.Validate(
            request,
            [
                CreateRange(
                    request,
                    0,
                    "her heritage from Hawaii",
                    "a hair teacher from a while")
            ]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(
                request,
                0,
                "her heritage from Hawaii",
                "a hair teacher from a while"));
    }

    [Fact]
    public void Validate_WhenBroadRangeHasAmbiguousFunctionWordAnchor_ShouldKeepRange()
    {
        var request = CreateSingleSourceRequest(
            "art shows a side of her",
            "are a chose aside for");
        var broadRange = CreateRange(
            request,
            0,
            "art shows a side of her",
            "are a chose aside for");

        var result = _validator.Validate(request, [broadRange]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(broadRange);
    }

    [Fact]
    public void Validate_WhenAmbiguousPhraseIsOverSplit_ShouldRejectSource()
    {
        var request = CreateSingleSourceRequest(
            "art shows a side of her",
            "are a chose aside for");
        var result = _validator.Validate(
            request,
            [
                CreateRange(request, 0, "art", "are"),
                CreateRange(request, 0, "shows", "chose"),
                CreateRange(request, 0, "side of her", "aside for")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void Validate_WhenSplitBoundariesIncludeReliableAnchor_ShouldRejectSource()
    {
        var request = CreateSingleSourceRequest(
            "her heritage from Hawaii and",
            "a hair teacher from a while and");

        var result = _validator.Validate(
            request,
            [
                CreateRange(
                    request,
                    0,
                    "her heritage from",
                    "a hair teacher"),
                CreateRange(
                    request,
                    0,
                    "Hawaii and",
                    "a while")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void Validate_WhenBroadRangeHasSimpleParallelFunctionWordAnchor_ShouldKeepSingleRange()
    {
        var request = CreateSingleSourceRequest(
            "cat and dog",
            "cot and dug");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "cat and dog", "cot and dug")]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(request, 0, "cat and dog", "cot and dug"));
    }

    [Fact]
    public void Validate_WhenBroadRangeHasUniqueContentWordAnchor_ShouldKeepSingleRange()
    {
        var request = CreateSingleSourceRequest(
            "cat near dog",
            "cot near dug");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "cat near dog", "cot near dug")]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(request, 0, "cat near dog", "cot near dug"));
    }

    [Fact]
    public void Validate_WhenBroadRangeHasUniqueMultiWordAnchor_ShouldKeepSingleRange()
    {
        var request = CreateSingleSourceRequest(
            "cat near the house dog",
            "cot near the house dug");

        var result = _validator.Validate(
            request,
            [
                CreateRange(
                    request,
                    0,
                    "cat near the house dog",
                    "cot near the house dug")
            ]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(
                request,
                0,
                "cat near the house dog",
                "cot near the house dug"));
    }

    [Fact]
    public void Validate_WhenBroadRangeHasDuplicateMatchingAnchors_ShouldRemainUnsplit()
    {
        var request = CreateSingleSourceRequest(
            "bad and old and road",
            "sad and new and roads");
        var broadRange = CreateRange(
            request,
            0,
            "bad and old and road",
            "sad and new and roads");

        var result = _validator.Validate(request, [broadRange]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(broadRange);
    }

    [Theory]
    [InlineData("and")]
    [InlineData("from")]
    [InlineData("in")]
    [InlineData("of")]
    [InlineData("the")]
    public void Validate_WhenShortParallelPhraseUsesFunctionWordAnchor_ShouldKeepSingleRange(
        string anchor)
    {
        var originalText = $"cat {anchor} dog";
        var userText = $"cot {anchor} dug";
        var request = CreateSingleSourceRequest(originalText, userText);

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, originalText, userText)]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(request, 0, originalText, userText));
    }

    [Fact]
    public void Validate_WhenCanonicalizedOutputIsValidatedAgain_ShouldBeIdempotent()
    {
        var request = CreateSingleSourceRequest(
            "her heritage from Hawaii",
            "a hair teacher from a while");
        var first = _validator.Validate(
            request,
            [
                CreateRange(
                    request,
                    0,
                    "her heritage from Hawaii",
                    "a hair teacher from a while")
            ]);

        var second = _validator.Validate(request, first.NormalizedRanges);

        first.IsValid.ShouldBeTrue();
        second.IsValid.ShouldBeTrue();
        second.NormalizedRanges.ShouldBe(first.NormalizedRanges);
    }

    [Fact]
    public void Validate_WhenOnlyUserRangeIncludesMatchingTrailingContext_ShouldTrimIt()
    {
        var request = CreateSingleSourceRequest(
            "Every day, she",
            "Everyday she");

        var result = _validator.Validate(
            request,
            [CreateRange(request, 0, "Every day", "Everyday she")]);

        result.IsValid.ShouldBeTrue();
        result.NormalizedRanges.Single().ShouldBe(
            CreateRange(request, 0, "Every day", "Everyday"));
    }

    [Fact]
    public void Validate_WhenRangesOverlapMonotonically_ShouldRejectSource()
    {
        var request = CreateSingleSourceRequest(
            "bad road",
            "sad roads");

        var result = _validator.Validate(
            request,
            [
                CreateRange(request, 0, "bad road", "sad roads"),
                CreateRange(request, 0, "road", "roads")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void Validate_WhenRangesCrossBetweenOriginalAndUser_ShouldRejectSource()
    {
        var request = CreateSingleSourceRequest(
            "cat dog",
            "dug cot");

        var result = _validator.Validate(
            request,
            [
                CreateRange(request, 0, "cat", "cot"),
                CreateRange(request, 0, "dog", "dug")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void Validate_WhenMultipleRangesIncludePunctuationDifference_ShouldRejectSource()
    {
        var request = CreateRetirementRequest();

        var result = _validator.Validate(
            request,
            [
                CreateRange(request, "advantage of", "advantages"),
                CreateRange(request, "employer’s", "employers")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void Validate_WhenMultipleRangesIncludeIdenticalRange_ShouldRejectSource()
    {
        var request = CreateSingleSourceRequest(
            "cat and sun",
            "cot and sun");

        var result = _validator.Validate(
            request,
            [
                CreateRange(request, 0, "cat", "cot"),
                CreateRange(request, 0, "sun", "sun")
            ]);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("invalid_action_ranges");
    }

    [Fact]
    public void ValidateDecisions_WithRepairedAcceptedAndRejectedSources_ShouldIsolateEachOutcome()
    {
        const string originalText = "without sun | cat | dog sun";
        const string userText = "without the sun | cot | fox sun";
        var request = new AiRefinementRequest(
            originalText,
            userText,
            [
                CreateSource(
                    originalText,
                    userText,
                    0,
                    "without sun",
                    "without the sun"),
                CreateSource(originalText, userText, 1, "cat", "cot"),
                CreateSource(originalText, userText, 2, "dog sun", "fox sun")
            ]);

        var result = _validator.ValidateDecisions(
            request,
            [
                new AiRefinementDecision(
                    0,
                    AiRefinementActions.Refine,
                    "genuine_insertion_or_omission",
                    [CreateRange(request, 0, "sun", "sun")]),
                new AiRefinementDecision(
                    1,
                    AiRefinementActions.Refine,
                    "genuine_word_difference",
                    [CreateRange(request, 1, "cat", "cot")]),
                new AiRefinementDecision(
                    2,
                    AiRefinementActions.Refine,
                    "genuine_word_difference",
                    [CreateRange(request, 2, "sun", "sun")])
            ]);

        result.IsValid.ShouldBeTrue();
        result.RejectedSources.Single().ShouldBe(
            new AiRefinementValidationIssue(2, "identical_selected_text"));
        result.Decisions.Single(decision => decision.SourceComparisonIndex == 0)
            .ValidationStatus.ShouldBe("accepted");
        result.Decisions.Single(decision => decision.SourceComparisonIndex == 1)
            .ValidationStatus.ShouldBe("accepted");
        result.Decisions.Single(decision => decision.SourceComparisonIndex == 2)
            .ValidationStatus.ShouldBe("rejected");
        result.Comparisons.Count.ShouldBe(3);
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

    private static AiRefinementRequest CreateRetirementRequest()
    {
        const string originalText =
            "advantage of their employer’s 401(k) match";
        const string userText =
            "advantages their employers 401k match";

        return new AiRefinementRequest(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    5,
                    new TextRange(0, originalText.Length - 1),
                    originalText,
                    new TextRange(0, userText.Length - 1),
                    userText)
            ]);
    }

    private static AiRefinementRequest CreateSingleSourceRequest(
        string originalText,
        string userText,
        int sourceComparisonIndex = 0) =>
        new(
            originalText,
            userText,
            [
                new AiRefinementSourceComparison(
                    sourceComparisonIndex,
                    new TextRange(0, originalText.Length - 1),
                    originalText,
                    new TextRange(0, userText.Length - 1),
                    userText)
            ]);

    private static AiRefinementSourceComparison CreateSource(
        string originalText,
        string userText,
        int sourceComparisonIndex,
        string originalSnippet,
        string userSnippet)
    {
        var originalStart = originalText.IndexOf(
            originalSnippet,
            StringComparison.Ordinal);
        var userStart = userText.IndexOf(
            userSnippet,
            StringComparison.Ordinal);

        return new AiRefinementSourceComparison(
            sourceComparisonIndex,
            new TextRange(
                originalStart,
                originalStart + originalSnippet.Length - 1),
            originalSnippet,
            new TextRange(
                userStart,
                userStart + userSnippet.Length - 1),
            userSnippet);
    }

    private static AiRefinedComparison CreateRange(
        AiRefinementRequest request,
        string originalText,
        string userText) =>
        CreateRange(request, 5, originalText, userText);

    private static AiRefinedComparison CreateRange(
        AiRefinementRequest request,
        int sourceComparisonIndex,
        string originalText,
        string userText)
    {
        var source = request.Comparisons.Single(
            comparison =>
                comparison.SourceComparisonIndex == sourceComparisonIndex);
        var originalStart = request.OriginalText.IndexOf(
            originalText,
            source.OriginalTextRange.InitialIndex,
            source.OriginalTextRange.FinalIndex
            - source.OriginalTextRange.InitialIndex
            + 1,
            StringComparison.Ordinal);
        var userStart = request.UserText.IndexOf(
            userText,
            source.UserTextRange.InitialIndex,
            source.UserTextRange.FinalIndex
            - source.UserTextRange.InitialIndex
            + 1,
            StringComparison.Ordinal);

        return new AiRefinedComparison(
            sourceComparisonIndex,
            originalStart,
            originalStart + originalText.Length - 1,
            userStart,
            userStart + userText.Length - 1);
    }
}

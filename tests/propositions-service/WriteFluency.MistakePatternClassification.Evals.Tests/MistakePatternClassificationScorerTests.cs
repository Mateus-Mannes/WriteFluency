using Shouldly;
using WriteFluency.MistakePatternClassification.Evals;
using WriteFluency.TextComparisons;

namespace WriteFluency.MistakePatternClassification.Evals.Tests;

public sealed class MistakePatternClassificationScorerTests
{
    [Fact]
    public void Score_ShouldPassWhenExpectedTagsAndPhraseAreCloseToReference()
    {
        var result = Score(
            expectedTags: ["verb_form"],
            actualTags: ["verb_form"],
            referencePhrase: "The original uses \"think\" as the main verb; \"thinking\" is an -ing form and would need a different sentence structure.",
            phrase: "The original uses \"think\" as the main verb; \"thinking\" is an -ing form that needs a different sentence structure.");

        result.Passed.ShouldBeTrue();
        result.TagsPassed.ShouldBeTrue();
        result.PhrasePassed.ShouldBeTrue();
    }

    [Fact]
    public void Score_ShouldFailWhenRequiredTagIsMissing()
    {
        var result = Score(
            expectedTags: ["verb_form"],
            actualTags: ["word_choice"],
            referencePhrase: "\"Sews\" means stitching fabric; \"saws\" means cutting with a tool.",
            phrase: "Listen for the verb ending because it changes the tense.");

        result.Passed.ShouldBeFalse();
        result.Failures.ShouldContain("missing_tags:verb_form");
    }

    [Fact]
    public void Score_ShouldFailWhenForbiddenTagIsPresent()
    {
        var result = Score(
            expectedTags: ["word_choice"],
            actualTags: ["word_choice", "spelling"],
            referencePhrase: "\"Sews\" means stitching fabric; \"saws\" means cutting with a tool.",
            phrase: "This changes the meaning of the phrase.",
            forbiddenTags: ["spelling"]);

        result.Passed.ShouldBeFalse();
        result.Failures.ShouldContain("forbidden_tags:spelling");
    }

    [Fact]
    public void Score_ShouldUseAcceptedTagsForPrecisionAndRecall()
    {
        var result = Score(
            expectedTags: ["word_boundary"],
            actualTags: ["word_boundary", "word_choice"],
            referencePhrase: "\"May be\" is a verb phrase, while \"maybe\" means perhaps.",
            phrase: "Word spacing changes the meaning here.",
            acceptedTags: ["word_boundary", "word_choice"]);

        result.TagsPassed.ShouldBeTrue();
        result.TagTruePositiveCount.ShouldBe(2);
        result.TagPredictedCount.ShouldBe(2);
        result.TagExpectedCount.ShouldBe(2);
    }

    [Fact]
    public void Score_ShouldFailPhraseThatOnlyRestatesDifference()
    {
        var result = Score(
            expectedTags: ["word_choice"],
            actualTags: ["word_choice"],
            referencePhrase: "\"Sews\" means stitching fabric; \"saws\" means cutting with a tool.",
            phrase: "You wrote saws, but the expected text is sews.");

        result.Passed.ShouldBeFalse();
        result.Failures.ShouldContain("phrase_restates_diff");
    }

    [Fact]
    public void Score_ShouldKeepLowSimilarityPhraseForAiGrading()
    {
        var result = Score(
            expectedTags: ["word_choice"],
            actualTags: ["word_choice"],
            referencePhrase: "\"Sews\" means stitching fabric; \"saws\" means cutting with a tool.",
            phrase: "Listen carefully to this part.");

        result.PhrasePassed.ShouldBeTrue();
        result.PhraseTokenF1.ShouldBeLessThan(0.80);
        result.PhraseEditSimilarity.ShouldBeLessThan(0.80);
    }

    [Fact]
    public void Score_ShouldAcceptLearnerOrientedTagAliases()
    {
        var result = Score(
            expectedTags: ["missing_or_extra_word"],
            actualTags: ["missing_word"],
            referencePhrase: "The original uses \"this\"; writing \"it\" swaps the pronoun in this phrase.",
            phrase: "Be careful not to drop small words because they connect the phrase.");

        result.TagsPassed.ShouldBeTrue();
    }

    [Fact]
    public void Score_ShouldPassSimilarPhraseByTokenF1()
    {
        var result = Score(
            expectedTags: ["word_choice"],
            actualTags: ["word_choice"],
            referencePhrase: "\"Sews\" means stitching fabric; \"saws\" means cutting with a tool.",
            phrase: "\"Sews\" means stitching fabric, while \"saws\" means cutting with a tool.");

        result.Passed.ShouldBeTrue();
        result.PhrasePassed.ShouldBeTrue();
        result.PhraseTokenF1.ShouldBeGreaterThanOrEqualTo(0.80);
    }

    [Fact]
    public void Score_ShouldAcceptModalVerbAliasForVerbForm()
    {
        var result = Score(
            expectedTags: ["modal_verb"],
            actualTags: ["verb_form"],
            referencePhrase: "\"Could\" suggests possibility, while \"will\" presents the event as more certain.",
            phrase: "Modal verbs like could and will show different certainty levels.");

        result.TagsPassed.ShouldBeTrue();
    }

    [Fact]
    public void Score_ShouldFailPhraseContainingTarget()
    {
        var result = Score(
            expectedTags: ["word_choice"],
            actualTags: ["word_choice"],
            referencePhrase: "\"Sews\" means stitching fabric; \"saws\" means cutting with a tool.",
            phrase: "The target word \"sews\" means stitching fabric.");

        result.Passed.ShouldBeFalse();
        result.Failures.ShouldContain("forbidden_phrase:target");
    }

    private static EvaluationComparisonResult Score(
        IReadOnlyList<string> expectedTags,
        IReadOnlyList<string> actualTags,
        string referencePhrase,
        string phrase,
        IReadOnlyList<string>? acceptedTags = null,
        IReadOnlyList<string>? forbiddenTags = null)
    {
        var evaluationCase = new EvaluationCase
        {
            CaseId = "case",
            Category = "category",
            OriginalText = "She sews fabric.",
            UserText = "She saws fabric.",
            Comparisons = []
        };
        var comparison = new EvaluationComparison
        {
            ComparisonIndex = 0,
            SourceComparisonIndex = 0,
            OriginalTextRange = new EvaluationTextRange(4, 7),
            OriginalText = "sews",
            UserTextRange = new EvaluationTextRange(4, 7),
            UserText = "saws",
            ExpectedTags = expectedTags.ToList(),
            ReferenceStudentPhrase = referencePhrase,
            AcceptedTags = acceptedTags?.ToList(),
            ForbiddenTags = forbiddenTags?.ToList()
        };

        return MistakePatternClassificationScorer.Score(
            evaluationCase,
            comparison,
            new MistakePatternAnnotation(
                0,
                0,
                actualTags.ToArray(),
                phrase));
    }
}

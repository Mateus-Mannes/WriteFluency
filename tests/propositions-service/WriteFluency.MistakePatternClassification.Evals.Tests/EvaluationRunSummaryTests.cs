using Shouldly;

namespace WriteFluency.MistakePatternClassification.Evals.Tests;

public sealed class EvaluationRunSummaryTests
{
    [Fact]
    public void Passed_ShouldFailWhenPhraseQualityIsBelowThreshold()
    {
        var summary = CreateSummary(
            [
                CreateComparison(
                    tagTruePositiveCount: 1,
                    tagPredictedCount: 1,
                    tagExpectedCount: 1,
                    phrasePassed: false)
            ]);

        summary.TagPrecision.ShouldBe(1);
        summary.TagRecall.ShouldBe(1);
        summary.PhrasePassRate.ShouldBe(0);
        summary.Passed.ShouldBeFalse();
    }

    [Fact]
    public void Passed_ShouldPassWhenTagAndPhraseQualityMeetThresholds()
    {
        var summary = CreateSummary(
            [
                CreateComparison(
                    tagTruePositiveCount: 1,
                    tagPredictedCount: 1,
                    tagExpectedCount: 1,
                    phrasePassed: true)
            ]);

        summary.Passed.ShouldBeTrue();
    }

    [Fact]
    public void EstimatedCostUsd_ShouldUseInputAndOutputTokenRates()
    {
        var summary = new EvaluationRunSummary(
            "test-model",
            0.2f,
            10,
            new EvaluationPricing(
                InputUsdPerMillionTokens: 0.10m,
                OutputUsdPerMillionTokens: 0.50m),
            DateTimeOffset.UtcNow,
            [
                new EvaluationCaseRunResult(
                    "case",
                    "category",
                    RunNumber: 1,
                    Passed: true,
                    DurationMilliseconds: 0,
                    Requests:
                    [
                        new EvaluationRequestResult(
                            "classifier",
                            BatchNumber: 1,
                            StartIndex: 0,
                            ComparisonCount: 1,
                            DurationMilliseconds: 0,
                            InputTokenCount: 1_000_000,
                            OutputTokenCount: 1_000_000,
                            TotalTokenCount: 2_000_000)
                    ],
                    Error: null,
                    Comparisons:
                    [
                        CreateComparison(
                            tagTruePositiveCount: 1,
                            tagPredictedCount: 1,
                            tagExpectedCount: 1,
                            phrasePassed: true)
                    ])
            ]);

        summary.EstimatedCostUsd.ShouldBe(0.60m);
    }

    private static EvaluationRunSummary CreateSummary(
        IReadOnlyList<EvaluationComparisonResult> comparisons) =>
        new(
            "test-model",
            0.2f,
            10,
            Pricing: null,
            DateTimeOffset.UtcNow,
            [
                new EvaluationCaseRunResult(
                    "case",
                    "category",
                    RunNumber: 1,
                    Passed: comparisons.All(comparison => comparison.Passed),
                    DurationMilliseconds: 0,
                    Requests: [],
                    Error: null,
                    Comparisons: comparisons)
            ]);

    private static EvaluationComparisonResult CreateComparison(
        int tagTruePositiveCount,
        int tagPredictedCount,
        int tagExpectedCount,
        bool phrasePassed) =>
        new(
            ComparisonIndex: 0,
            SourceComparisonIndex: 0,
            OriginalText: "owners",
            UserText: "owner",
            OriginalContext: new EvaluationContextSnippet("", "owners", ""),
            UserContext: new EvaluationContextSnippet("", "owner", ""),
            ExpectedTags: ["singular_plural"],
            AcceptedTags: ["singular_plural"],
            ForbiddenTags: [],
            ActualTags: ["singular_plural"],
            ActualPhrase: phrasePassed
                ? "\"Owners\" is plural; \"owner\" is singular."
                : "Different phrase.",
            ReferenceStudentPhrase: "\"Owners\" is plural; \"owner\" is singular.",
            PhraseTokenF1: phrasePassed ? 1 : 0,
            PhraseEditSimilarity: phrasePassed ? 1 : 0,
            PhraseAiSimilarityScore: phrasePassed ? 1 : 0.2,
            PhraseAiSimilarityReason: phrasePassed
                ? "The actual phrase preserves the same plural lesson."
                : "The actual phrase does not teach the same lesson.",
            TagsPassed: tagTruePositiveCount == tagExpectedCount,
            PhrasePassed: phrasePassed,
            Passed: tagTruePositiveCount == tagExpectedCount && phrasePassed,
            Failures: phrasePassed ? [] : ["phrase_similarity_below_threshold:token_f1=0.000,edit=0.000"],
            TagTruePositiveCount: tagTruePositiveCount,
            TagPredictedCount: tagPredictedCount,
            TagExpectedCount: tagExpectedCount);
}

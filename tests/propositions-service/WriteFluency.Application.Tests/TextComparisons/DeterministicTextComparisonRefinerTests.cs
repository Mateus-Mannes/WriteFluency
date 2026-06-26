using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public sealed class DeterministicTextComparisonRefinerTests
{
    private readonly DeterministicTextComparisonRefiner _refiner =
        new(new DeterministicTextEquivalenceService(
            new EnglishNumberNormalizer()));

    [Theory]
    [InlineData("cozy", "cosy")]
    [InlineData("1500s", "1500's")]
    [InlineData("woodwork", "wood work")]
    [InlineData("in 2022", "in twenty twenty two")]
    [InlineData("want to", "wanna")]
    public void Refine_WhenFullRangeIsEquivalent_ShouldRemoveComparison(
        string originalText,
        string userText)
    {
        var result = _refiner.Refine(
            originalText,
            userText,
            [CreateComparison(originalText, userText)]);

        result.Comparisons.ShouldBeEmpty();
        result.RemovedComparisonCount.ShouldBe(1);
        result.HasChanges.ShouldBeTrue();
        result.Trace.Single().Value.Deterministic.ShouldNotBeNull();
        result.Trace.Single().Value.Deterministic!.Action.ShouldBe(
            AiRefinementActions.Remove);
        result.Trace.Single().Value.Deterministic!.Output.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(
        "in energy, healthcare, and",
        "and energy, health care, and",
        "in",
        "and")]
    [InlineData(
        "aging without sun",
        "ageing without the sun",
        "sun",
        "the sun")]
    [InlineData(
        "record of Saudi Aramco",
        "record by of Saudi Aramco",
        "of",
        "by of")]
    public void Refine_WhenBoundaryTextIsEquivalent_ShouldShrinkToGenuineError(
        string originalText,
        string userText,
        string expectedOriginal,
        string expectedUser)
    {
        var result = _refiner.Refine(
            originalText,
            userText,
            [CreateComparison(originalText, userText)]);

        result.Comparisons.Single().OriginalText.ShouldBe(expectedOriginal);
        result.Comparisons.Single().UserText.ShouldBe(expectedUser);
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeTrue();
        result.Trace.Single().Value.Deterministic.ShouldNotBeNull();
        result.Trace.Single().Value.Deterministic!.Action.ShouldBe(
            AiRefinementActions.Refine);
    }

    [Fact]
    public void Refine_WhenOnlyPrefixIsEquivalent_ShouldShrinkPrefix()
    {
        var result = _refiner.Refine(
            "healthcare leads",
            "health care led",
            [CreateComparison("healthcare leads", "health care led")]);

        result.Comparisons.Single().OriginalText.ShouldBe("leads");
        result.Comparisons.Single().UserText.ShouldBe("led");
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeTrue();
    }

    [Fact]
    public void Refine_WhenOnlySuffixIsEquivalent_ShouldShrinkSuffix()
    {
        var result = _refiner.Refine(
            "in healthcare",
            "and health care",
            [CreateComparison("in healthcare", "and health care")]);

        result.Comparisons.Single().OriginalText.ShouldBe("in");
        result.Comparisons.Single().UserText.ShouldBe("and");
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeTrue();
    }

    [Fact]
    public void Refine_WhenPrefixAndSuffixAreEquivalent_ShouldShrinkBothSides()
    {
        var result = _refiner.Refine(
            "the healthcare market rose",
            "the health care markets rose",
            [CreateComparison(
                "the healthcare market rose",
                "the health care markets rose")]);

        result.Comparisons.Single().OriginalText.ShouldBe("market");
        result.Comparisons.Single().UserText.ShouldBe("markets");
        result.Trace.Single().Value.Deterministic!.Output.Single()
            .OriginalText.ShouldBe("market");
        result.Trace.Single().Value.Deterministic!.Output.Single()
            .UserText.ShouldBe("markets");
    }

    [Theory]
    [InlineData(
        "contributions to your retirement",
        "contribution to retirement",
        new[] { "contributions", "your retirement" },
        new[] { "contribution", "retirement" })]
    [InlineData(
        "workers who clear their mortgages or",
        "workers clear their or",
        new[] { "workers who", "mortgages or" },
        new[] { "workers", "or" })]
    public void Refine_WhenReliableAnchorSeparatesErrors_ShouldSplit(
        string originalText,
        string userText,
        string[] expectedOriginal,
        string[] expectedUser)
    {
        var result = _refiner.Refine(
            originalText,
            userText,
            [CreateComparison(originalText, userText)]);

        result.Comparisons.Select(comparison => comparison.OriginalText)
            .ShouldBe(expectedOriginal);
        result.Comparisons.Select(comparison => comparison.UserText)
            .ShouldBe(expectedUser);
        result.Comparisons.ShouldAllBe(comparison =>
            comparison.IsDeterministicallyRefined);
        result.Trace.Single().Value.Deterministic!.Output.Count.ShouldBe(
            expectedOriginal.Length);
    }

    [Fact]
    public void Refine_WhenEquivalentBoundaryHidesUniqueAnchor_ShouldShrinkThenSplit()
    {
        var result = _refiner.Refine(
            "near cat near dog",
            "near cot near dug",
            [CreateComparison("near cat near dog", "near cot near dug")]);

        result.Comparisons.Select(comparison => comparison.OriginalText)
            .ShouldBe(["cat", "dog"]);
        result.Comparisons.Select(comparison => comparison.UserText)
            .ShouldBe(["cot", "dug"]);
    }

    [Fact]
    public void Refine_WhenSplitSideCanSplitAgain_ShouldRecursivelySplit()
    {
        var result = _refiner.Refine(
            "bad alpha beta cat near dog",
            "sad alpha beta cot near dug",
            [CreateComparison(
                "bad alpha beta cat near dog",
                "sad alpha beta cot near dug")]);

        result.Comparisons.Select(comparison => comparison.OriginalText)
            .ShouldBe(["bad", "cat", "dog"]);
        result.Comparisons.Select(comparison => comparison.UserText)
            .ShouldBe(["sad", "cot", "dug"]);
    }

    [Fact]
    public void Refine_WhenAnchorIsEquivalentButNotExact_ShouldSplit()
    {
        var result = _refiner.Refine(
            "cat health care dog",
            "cot healthcare dug",
            [CreateComparison("cat health care dog", "cot healthcare dug")]);

        result.Comparisons.Select(comparison => comparison.OriginalText)
            .ShouldBe(["cat", "dog"]);
        result.Comparisons.Select(comparison => comparison.UserText)
            .ShouldBe(["cot", "dug"]);
    }

    [Fact]
    public void Refine_WhenShortPhraseHasAlignedFunctionAnchor_ShouldSplit()
    {
        var result = _refiner.Refine(
            "cat and dog",
            "cot and dug",
            [CreateComparison("cat and dog", "cot and dug")]);

        result.Comparisons.Select(comparison => comparison.OriginalText)
            .ShouldBe(["cat", "dog"]);
        result.Comparisons.Select(comparison => comparison.UserText)
            .ShouldBe(["cot", "dug"]);
    }

    [Theory]
    [InlineData("She can", "can she")]
    [InlineData("art shows a side of her", "are a chose aside for")]
    [InlineData("quiet roads", "quite rows")]
    [InlineData("bad and old and road", "sad and new and roads")]
    [InlineData("cat and very old dog", "cot and really new dug")]
    [InlineData("bad break-ins road", "sad break in roads")]
    public void Refine_WhenSplitWouldBeAmbiguous_ShouldKeepSingleComparison(
        string originalText,
        string userText)
    {
        var result = _refiner.Refine(
            originalText,
            userText,
            [CreateComparison(originalText, userText)]);

        result.Comparisons.Single().OriginalText.ShouldBe(originalText);
        result.Comparisons.Single().UserText.ShouldBe(userText);
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeFalse();
        result.HasChanges.ShouldBeFalse();
        result.Trace.ShouldBeEmpty();
    }

    [Fact]
    public void Refine_WhenSplitWouldProduceEquivalentSide_ShouldShrinkInstead()
    {
        var result = _refiner.Refine(
            "cat near dog",
            "cot near dog",
            [CreateComparison("cat near dog", "cot near dog")]);

        result.Comparisons.Single().OriginalText.ShouldBe("cat");
        result.Comparisons.Single().UserText.ShouldBe("cot");
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeTrue();
    }

    [Fact]
    public void Refine_WhenMultipleSourcesAreProvided_ShouldPreserveSourceIndexes()
    {
        const string originalText = "cat near dog value";
        const string userText = "cot near dug values";

        var result = _refiner.Refine(
            originalText,
            userText,
            [
                CreateComparison(
                    originalText,
                    userText,
                    "cat near dog",
                    "cot near dug",
                    2),
                CreateComparison(
                    originalText,
                    userText,
                    "value",
                    "values",
                    9)
            ]);

        result.Comparisons.Select(comparison => comparison.SourceComparisonIndex)
            .ShouldBe([2, 2, 9]);
        result.Comparisons.Select(comparison => comparison.OriginalText)
            .ShouldBe(["cat", "dog", "value"]);
        result.Trace.Keys.Order().ShouldBe([2]);
    }

    [Fact]
    public void Refine_WhenAppliedToItsOwnOutput_ShouldBeIdempotent()
    {
        const string originalText = "the healthcare cat near dog rose";
        const string userText = "the health care cot near dug rose";
        var first = _refiner.Refine(
            originalText,
            userText,
            [CreateComparison(originalText, userText)]);

        var second = _refiner.Refine(
            originalText,
            userText,
            first.Comparisons);

        second.Comparisons.Select(comparison => comparison.OriginalText)
            .ShouldBe(first.Comparisons.Select(comparison =>
                comparison.OriginalText));
        second.Comparisons.Select(comparison => comparison.UserText)
            .ShouldBe(first.Comparisons.Select(comparison =>
                comparison.UserText));
        second.HasChanges.ShouldBeFalse();
        second.Trace.ShouldBeEmpty();
    }

    [Fact]
    public void Refine_WhenComparisonIsUnchanged_ShouldPreserveProvenance()
    {
        var result = _refiner.Refine(
            "value",
            "values",
            [CreateComparison("value", "values")]);

        result.Comparisons.Single().SourceComparisonIndex.ShouldBe(4);
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeFalse();
        result.Comparisons.Single().IsAiRefined.ShouldBeFalse();
        result.Trace.ShouldBeEmpty();
    }

    private static TextComparison CreateComparison(
        string originalText,
        string userText,
        int sourceComparisonIndex = 4) =>
        new(
            new TextRange(0, originalText.Length - 1),
            originalText,
            new TextRange(0, userText.Length - 1),
            userText,
            sourceComparisonIndex);

    private static TextComparison CreateComparison(
        string originalFullText,
        string userFullText,
        string originalSnippet,
        string userSnippet,
        int sourceComparisonIndex)
    {
        var originalStart = originalFullText.IndexOf(
            originalSnippet,
            StringComparison.Ordinal);
        var userStart = userFullText.IndexOf(
            userSnippet,
            StringComparison.Ordinal);

        originalStart.ShouldBeGreaterThanOrEqualTo(0);
        userStart.ShouldBeGreaterThanOrEqualTo(0);

        return new TextComparison(
            new TextRange(
                originalStart,
                originalStart + originalSnippet.Length - 1),
            originalSnippet,
            new TextRange(userStart, userStart + userSnippet.Length - 1),
            userSnippet,
            sourceComparisonIndex);
    }
}

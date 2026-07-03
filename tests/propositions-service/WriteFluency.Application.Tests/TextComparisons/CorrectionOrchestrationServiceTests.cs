using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public class CorrectionOrchestrationServiceTests
{
    [Fact]
    public async Task CompareTextsAsync_ForFreeUser_ShouldReturnStaticComparison()
    {
        var service = CreateService();

        var orchestrationResult = await service.CompareTextsAsync(
            "Kate’s work",
            "Kate's work",
            isPro: false,
            CancellationToken.None);
        var result = orchestrationResult.Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Static);
        result.Comparisons.ShouldNotBeEmpty();
        result.Comparisons.ShouldAllBe(comparison =>
            comparison.MistakePatternTags == null
            && comparison.MistakePatternPhrase == null);
        orchestrationResult.StaticComparisonCount.ShouldBe(1);
        orchestrationResult.RemovedComparisonCount.ShouldBe(0);
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUserWithOnlyEquivalentComparison_ShouldReturnNormalizedResult()
    {
        var service = CreateService();

        var orchestrationResult = await service.CompareTextsAsync(
            "Kate’s work",
            "Kate's work",
            isPro: true,
            CancellationToken.None);
        var result = orchestrationResult.Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Normalized);
        result.Comparisons.ShouldBeEmpty();
        result.CorrectionTrace.ShouldNotBeNull();
        var trace = result.CorrectionTrace.Single();
        trace.SourceComparisonIndex.ShouldBe(0);
        trace.Initial.OriginalText.ShouldNotBeEmpty();
        trace.Deterministic.ShouldNotBeNull();
        trace.Deterministic.Action.ShouldBe(CorrectionRefinementActions.Remove);
        orchestrationResult.StaticComparisonCount.ShouldBe(1);
        orchestrationResult.RemovedComparisonCount.ShouldBe(1);
        result.AccuracyPercentage.ShouldBe(1);
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUserWithUnresolvedComparison_ShouldReturnNormalizedResult()
    {
        var service = CreateService();

        var orchestrationResult = await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None);
        var result = orchestrationResult.Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Normalized);
        result.Comparisons.Count.ShouldBe(1);
        result.Comparisons[0].OriginalText!.ShouldContain("may be");
        result.Comparisons[0].UserText!.ShouldContain("maybe");
        result.Comparisons[0].IsDeterministicallyRefined.ShouldBeTrue();
        result.CorrectionTrace.ShouldNotBeNull();
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUserWithFinalComparisons_ShouldAttachMistakePatternMetadata()
    {
        var classifier = new RecordingMistakePatternClassifier(
            request => request.Comparisons
                .Select((comparison, index) => new MistakePatternAnnotation(
                    index,
                    comparison.SourceComparisonIndex,
                    ["word_boundary", "word_choice"],
                    "Word spacing changes the meaning here."))
                .ToArray());
        var service = CreateService(mistakePatternClassifier: classifier);

        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None)).Result;

        classifier.CallCount.ShouldBe(1);
        var comparison = result.Comparisons.Single();
        comparison.MistakePatternTags.ShouldBe(["word_boundary", "word_choice"]);
        comparison.MistakePatternPhrase.ShouldBe("Word spacing changes the meaning here.");
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUser_WhenClassifierFails_ShouldReturnResultWithoutMistakePatternMetadata()
    {
        var service = CreateService(
            mistakePatternClassifier: new ThrowingMistakePatternClassifier());

        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None)).Result;

        result.Comparisons.ShouldNotBeEmpty();
        result.Comparisons.ShouldAllBe(comparison =>
            comparison.MistakePatternTags == null
            && comparison.MistakePatternPhrase == null);
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUser_ShouldSanitizeMistakePatternMetadata()
    {
        var classifier = new RecordingMistakePatternClassifier(_ =>
        [
            new MistakePatternAnnotation(
                0,
                0,
                [" spelling ", "SPELLING", "word_choice", "extra_word"],
                "  Useful phrase.  "),
            new MistakePatternAnnotation(
                0,
                999,
                ["word_choice"],
                "Invalid source."),
            new MistakePatternAnnotation(
                1,
                0,
                [],
                "Missing tags.")
        ]);
        var service = CreateService(mistakePatternClassifier: classifier);

        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None)).Result;

        var comparison = result.Comparisons.Single();
        comparison.MistakePatternTags.ShouldBe(["spelling", "word_choice", "extra_word"]);
        comparison.MistakePatternPhrase.ShouldBe("Useful phrase.");
    }

    [Fact]
    public async Task CompareTextsAsync_WhenDeterministicRefinerShrinksComparison_ShouldReturnNormalizedResult()
    {
        var service = CreateService();

        var result = (await service.CompareTextsAsync(
            "in energy, healthcare, and",
            "and energy, health care, and",
            isPro: true,
            CancellationToken.None)).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Normalized);
        result.Comparisons.Single().OriginalText.ShouldBe("in");
        result.Comparisons.Single().UserText.ShouldBe("and");
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeTrue();
        result.CorrectionTrace.ShouldNotBeNull();
    }

    [Fact]
    public async Task CompareTextsAsync_WhenRequestIsCanceled_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var service = CreateService();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.CompareTextsAsync(
                "They may be ready",
                "They maybe ready",
                isPro: true,
                cancellation.Token));
    }

    private static CorrectionOrchestrationService CreateService(
        IMistakePatternClassifier? mistakePatternClassifier = null)
    {
        return new CorrectionOrchestrationService(
            CreateTextComparisonService(),
            CreateDeterministicRefiner(),
            mistakePatternClassifier ?? new EmptyMistakePatternClassifier());
    }

    private static DeterministicTextComparisonRefiner CreateDeterministicRefiner() =>
        new(new DeterministicTextEquivalenceService(
            new EnglishNumberNormalizer()));

    private static TextComparisonService CreateTextComparisonService()
    {
        var levenshteinDistanceService = new LevenshteinDistanceService();
        return new TextComparisonService(
            levenshteinDistanceService,
            new TextAlignmentService(
                new NeedlemanWunschAlignmentService(levenshteinDistanceService),
                new TokenizeTextService(),
                new TokenAlignmentService()),
            new TokenComparisonService());
    }

    private sealed class RecordingMistakePatternClassifier : IMistakePatternClassifier
    {
        private readonly Func<MistakePatternClassificationRequest, IReadOnlyList<MistakePatternAnnotation>>
            _handler;

        public int CallCount { get; private set; }

        public RecordingMistakePatternClassifier(
            Func<MistakePatternClassificationRequest, IReadOnlyList<MistakePatternAnnotation>> handler)
        {
            _handler = handler;
        }

        public Task<IReadOnlyList<MistakePatternAnnotation>> ClassifyAsync(
            MistakePatternClassificationRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class EmptyMistakePatternClassifier : IMistakePatternClassifier
    {
        public Task<IReadOnlyList<MistakePatternAnnotation>> ClassifyAsync(
            MistakePatternClassificationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MistakePatternAnnotation>>([]);
    }

    private sealed class ThrowingMistakePatternClassifier : IMistakePatternClassifier
    {
        public Task<IReadOnlyList<MistakePatternAnnotation>> ClassifyAsync(
            MistakePatternClassificationRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Classifier failed.");
    }
}

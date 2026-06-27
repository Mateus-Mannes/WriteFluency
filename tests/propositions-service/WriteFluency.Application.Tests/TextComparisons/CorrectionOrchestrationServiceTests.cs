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
        result.AiAttempted.ShouldBeFalse();
        result.Comparisons.ShouldNotBeEmpty();
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
        result.AiAttempted.ShouldBeFalse();
        result.Comparisons.ShouldBeEmpty();
        result.CorrectionTrace.ShouldNotBeNull();
        var trace = result.CorrectionTrace.Single();
        trace.SourceComparisonIndex.ShouldBe(0);
        trace.Initial.OriginalText.ShouldNotBeEmpty();
        trace.Deterministic.ShouldNotBeNull();
        trace.Deterministic.Action.ShouldBe(AiRefinementActions.Remove);
        trace.Ai.ShouldBeNull();
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
        result.AiAttempted.ShouldBeFalse();
        result.Comparisons.Count.ShouldBe(1);
        result.Comparisons[0].OriginalText!.ShouldContain("may be");
        result.Comparisons[0].UserText!.ShouldContain("maybe");
        result.Comparisons[0].IsDeterministicallyRefined.ShouldBeTrue();
        result.Comparisons[0].IsAiRefined.ShouldBeFalse();
        result.CorrectionTrace.ShouldNotBeNull();
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
        result.AiAttempted.ShouldBeFalse();
        result.Comparisons.Single().OriginalText.ShouldBe("in");
        result.Comparisons.Single().UserText.ShouldBe("and");
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeTrue();
        result.Comparisons.Single().IsAiRefined.ShouldBeFalse();
        result.CorrectionTrace.ShouldNotBeNull();
        result.CorrectionTrace.Single().Deterministic!.Action.ShouldBe(
            AiRefinementActions.Refine);
        result.CorrectionTrace.Single().Ai.ShouldBeNull();
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

    private static CorrectionOrchestrationService CreateService()
    {
        return new CorrectionOrchestrationService(
            CreateTextComparisonService(),
            CreateDeterministicRefiner());
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
}

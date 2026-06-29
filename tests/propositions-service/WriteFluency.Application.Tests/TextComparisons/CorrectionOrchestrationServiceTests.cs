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
        orchestrationResult.StaticComparisonCount.ShouldBe(1);
        orchestrationResult.RemovedComparisonCount.ShouldBe(0);
        orchestrationResult.ValidationReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.Valid);
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
        result.CorrectionTrace.Single().Deterministic!.Action.ShouldBe(
            CorrectionRefinementActions.Refine);
    }

    [Fact]
    public async Task CompareTextsAsync_WhenDiffIsUnstable_ShouldSkipDeterministicRefinement()
    {
        var service = CreateService(new TextComparisonRefinementValidationOptions
        {
            MinStaticAccuracyPercentage = 0.99
        });

        var orchestrationResult = await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None);

        orchestrationResult.Result.CorrectionMode.ShouldBe(CorrectionModes.Static);
        orchestrationResult.Result.Comparisons.Single()
            .IsDeterministicallyRefined.ShouldBeFalse();
        orchestrationResult.ValidationReasonCode.ShouldBe(
            TextComparisonRefinementValidationReasons.SkipUnstableDiff);
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
        TextComparisonRefinementValidationOptions? options = null)
    {
        return new CorrectionOrchestrationService(
            CreateTextComparisonService(),
            CreateDeterministicRefiner(),
            new TextComparisonRefinementValidator(
                options ?? new TextComparisonRefinementValidationOptions
                {
                    MaxOriginalCoverageRatio = 1,
                    MinStaticAccuracyPercentage = 0
                }));
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

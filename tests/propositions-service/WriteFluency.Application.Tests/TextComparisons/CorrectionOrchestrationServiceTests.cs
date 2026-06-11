using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public class CorrectionOrchestrationServiceTests
{
    private readonly CorrectionOrchestrationService _service = CreateService();

    [Fact]
    public void CompareTexts_ForFreeUser_ShouldReturnStaticComparisonUnchanged()
    {
        var result = _service.CompareTexts("Kate’s work", "Kate's work", isPro: false).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Static);
        result.AiAttempted.ShouldBeFalse();
        result.Comparisons.ShouldNotBeEmpty();
    }

    [Fact]
    public void CompareTexts_ForProUser_ShouldRemoveEquivalentWholeComparison()
    {
        var orchestrationResult = _service.CompareTexts("Kate’s work", "Kate's work", isPro: true);
        var result = orchestrationResult.Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Normalized);
        result.AiAttempted.ShouldBeFalse();
        result.Comparisons.ShouldBeEmpty();
        orchestrationResult.StaticComparisonCount.ShouldBe(1);
        orchestrationResult.RemovedComparisonCount.ShouldBe(1);
        result.AccuracyPercentage.ShouldBe(1);
        result.OriginalText.ShouldBe("Kate’s work");
        result.UserText.ShouldBe("Kate's work");
    }

    [Fact]
    public void CompareTexts_ForProUser_ShouldPreserveNonEquivalentComparisonAndRanges()
    {
        var staticService = CreateTextComparisonService();
        var staticResult = staticService.CompareTexts("They may be ready", "They maybe ready");
        var expectedComparison = staticResult.Comparisons.Single();

        var result = _service.CompareTexts("They may be ready", "They maybe ready", isPro: true).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Static);
        result.Comparisons.Count.ShouldBe(1);
        result.Comparisons[0].OriginalText.ShouldBe(expectedComparison.OriginalText);
        result.Comparisons[0].UserText.ShouldBe(expectedComparison.UserText);
        result.Comparisons[0].OriginalTextRange.ShouldBe(expectedComparison.OriginalTextRange);
        result.Comparisons[0].UserTextRange.ShouldBe(expectedComparison.UserTextRange);
        result.AccuracyPercentage.ShouldBe(staticResult.AccuracyPercentage);
    }

    [Fact]
    public void CompareTexts_ForProUser_ShouldRemoveOnlyEquivalentComparisons()
    {
        var result = _service.CompareTexts(
            "Kate’s work may be difficult",
            "Kate's work maybe difficult",
            isPro: true).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Normalized);
        result.Comparisons.Count.ShouldBe(1);
        result.Comparisons[0].OriginalText.ShouldNotBeNull();
        result.Comparisons[0].UserText.ShouldNotBeNull();
        result.Comparisons[0].OriginalText!.ShouldContain("may be");
        result.Comparisons[0].UserText!.ShouldContain("maybe");
        result.AccuracyPercentage.ShouldBeGreaterThan(0);
    }

    private static CorrectionOrchestrationService CreateService()
    {
        return new CorrectionOrchestrationService(
            CreateTextComparisonService(),
            new DeterministicTextEquivalenceService(new EnglishNumberNormalizer()));
    }

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

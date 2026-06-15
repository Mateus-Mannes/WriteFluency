using NSubstitute;
using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public class CorrectionOrchestrationServiceTests
{
    [Fact]
    public async Task CompareTextsAsync_ForFreeUser_ShouldReturnStaticComparisonWithoutCallingAi()
    {
        var aiRefiner = CreateAiRefiner();
        var service = CreateService(aiRefiner);

        var result = (await service.CompareTextsAsync(
            "Kate’s work",
            "Kate's work",
            isPro: false,
            CancellationToken.None)).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Static);
        result.AiAttempted.ShouldBeFalse();
        result.Comparisons.ShouldNotBeEmpty();
        await aiRefiner.DidNotReceiveWithAnyArgs()
            .RefineAsync(default!, default);
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUserWithOnlyEquivalentComparison_ShouldSkipAi()
    {
        var aiRefiner = CreateAiRefiner();
        var service = CreateService(aiRefiner);

        var orchestrationResult = await service.CompareTextsAsync(
            "Kate’s work",
            "Kate's work",
            isPro: true,
            CancellationToken.None);
        var result = orchestrationResult.Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Normalized);
        result.AiAttempted.ShouldBeFalse();
        result.Comparisons.ShouldBeEmpty();
        orchestrationResult.StaticComparisonCount.ShouldBe(1);
        orchestrationResult.RemovedComparisonCount.ShouldBe(1);
        result.AccuracyPercentage.ShouldBe(1);
        await aiRefiner.DidNotReceiveWithAnyArgs()
            .RefineAsync(default!, default);
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUserWithUnresolvedComparison_ShouldApplyAiResult()
    {
        var aiRefiner = CreateAiRefiner();
        aiRefiner
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AiRefinementRequest>();
                var source = request.Comparisons.Single();
                return CreateRefinement(
                [
                    new AiRefinedComparison(
                        source.SourceComparisonIndex,
                        source.OriginalTextRange.InitialIndex,
                        source.OriginalTextRange.FinalIndex,
                        source.UserTextRange.InitialIndex,
                        source.UserTextRange.FinalIndex)
                ]);
            });

        var service = CreateService(aiRefiner);
        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None)).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.AiRefined);
        result.AiAttempted.ShouldBeTrue();
        result.Comparisons.Count.ShouldBe(1);
        result.Comparisons[0].OriginalText.ShouldNotBeNull();
        result.Comparisons[0].UserText.ShouldNotBeNull();
        result.Comparisons[0].OriginalText!.ShouldContain("may be");
        result.Comparisons[0].UserText!.ShouldContain("maybe");
        await aiRefiner.Received(1)
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompareTextsAsync_WhenAiRemovesAllComparisons_ShouldReturnPerfectAccuracy()
    {
        var aiRefiner = CreateAiRefiner();
        aiRefiner
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateRefinement([]));

        var service = CreateService(aiRefiner);
        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None)).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.AiRefined);
        result.AiAttempted.ShouldBeTrue();
        result.Comparisons.ShouldBeEmpty();
        result.AccuracyPercentage.ShouldBe(1);
    }

    [Fact]
    public async Task CompareTextsAsync_WhenAiOutputIsOutsideSource_ShouldReturnFallback()
    {
        var aiRefiner = CreateAiRefiner();
        aiRefiner
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AiRefinementRequest>();
                var source = request.Comparisons.Single();
                return CreateRefinement(
                [
                    new AiRefinedComparison(
                        source.SourceComparisonIndex,
                        0,
                        request.OriginalText.Length - 1,
                        0,
                        request.UserText.Length - 1)
                ]);
            });

        var service = CreateService(aiRefiner);
        var result = await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None);

        result.Result.CorrectionMode.ShouldBe(CorrectionModes.Fallback);
        result.Result.AiAttempted.ShouldBeTrue();
        result.Result.Comparisons.ShouldNotBeEmpty();
        result.AiValidationFailureReason.ShouldBe("range_outside_source");
    }

    [Fact]
    public async Task CompareTextsAsync_WhenAiThrows_ShouldReturnFallback()
    {
        var aiRefiner = CreateAiRefiner();
        aiRefiner
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiRefinementResult>>(_ => throw new InvalidOperationException("AI failed"));

        var service = CreateService(aiRefiner);
        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            CancellationToken.None)).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Fallback);
        result.AiAttempted.ShouldBeTrue();
        result.Comparisons.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CompareTextsAsync_WhenRequestIsCanceled_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var aiRefiner = CreateAiRefiner();
        aiRefiner
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiRefinementResult>>(_ =>
                throw new OperationCanceledException(cancellation.Token));

        var service = CreateService(aiRefiner);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.CompareTextsAsync(
                "They may be ready",
                "They maybe ready",
                isPro: true,
                cancellation.Token));
    }

    private static ITextComparisonAiRefiner CreateAiRefiner()
    {
        var aiRefiner = Substitute.For<ITextComparisonAiRefiner>();
        aiRefiner.Model.Returns("test-model");
        aiRefiner.PromptVersion.Returns("test-prompt");
        return aiRefiner;
    }

    private static AiRefinementResult CreateRefinement(
        IReadOnlyList<AiRefinedComparison> comparisons) =>
        new(
            comparisons,
            "test-model",
            "test-prompt",
            DurationMilliseconds: 10,
            InputTokenCount: 100,
            OutputTokenCount: 20);

    private static CorrectionOrchestrationService CreateService(
        ITextComparisonAiRefiner aiRefiner)
    {
        return new CorrectionOrchestrationService(
            CreateTextComparisonService(),
            new DeterministicTextEquivalenceService(new EnglishNumberNormalizer()),
            aiRefiner,
            new AiRefinementOutputValidator());
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

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
        result.Comparisons[0].SourceComparisonIndex.ShouldBe(0);
        result.Comparisons[0].IsDeterministicallyRefined.ShouldBeTrue();
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
            .Returns(call =>
            {
                var request = call.Arg<AiRefinementRequest>();
                return CreateRefinement(request.Comparisons.Select(source =>
                    new AiRefinementDecision(
                        source.SourceComparisonIndex,
                        AiRefinementActions.Remove,
                        "equivalent_transcription",
                        [])).ToList());
            });

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
        result.CorrectionTrace.ShouldNotBeNull();
        var trace = result.CorrectionTrace.Single();
        trace.Deterministic.ShouldNotBeNull();
        trace.Ai.ShouldNotBeNull();
        trace.Ai.Action.ShouldBe(AiRefinementActions.Remove);
        trace.Ai.Output.ShouldBeEmpty();
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
    public async Task CompareTextsAsync_WhenOneAiSourceIsInvalid_ShouldKeepValidAiWorkAndFallbackOnlyRejectedSource()
    {
        var aiRefiner = CreateAiRefiner();
        aiRefiner
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AiRefinementRequest>();
                request.Comparisons.Count.ShouldBe(2);
                var first = request.Comparisons[0];
                var second = request.Comparisons[1];

                return CreateRefinement(
                [
                    new AiRefinedComparison(
                        first.SourceComparisonIndex,
                        first.OriginalTextRange.InitialIndex,
                        first.OriginalTextRange.FinalIndex,
                        first.UserTextRange.InitialIndex,
                        first.UserTextRange.FinalIndex),
                    new AiRefinedComparison(
                        second.SourceComparisonIndex,
                        0,
                        request.OriginalText.Length - 1,
                        0,
                        request.UserText.Length - 1)
                ]);
            });

        var service = CreateService(aiRefiner);
        var result = await service.CompareTextsAsync(
            "A cat rests while a dog runs",
            "A cot rests while a dug runs",
            isPro: true,
            CancellationToken.None);

        result.Result.CorrectionMode.ShouldBe(CorrectionModes.AiRefined);
        result.Result.AiAttempted.ShouldBeTrue();
        result.Result.Comparisons.Count.ShouldBe(2);
        result.AiRejectedSourceComparisonCount.ShouldBe(1);
        result.AiValidationFailureReason.ShouldBe("range_outside_source");
        result.Result.Comparisons.ShouldContain(comparison =>
            comparison.OriginalText!.Contains("cat")
            && comparison.UserText!.Contains("cot"));
        result.Result.Comparisons.ShouldContain(comparison =>
            comparison.OriginalText!.Contains("dog")
            && comparison.UserText!.Contains("dug"));
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
    public async Task CompareTextsAsync_WhenDeterministicRefinerShrinksComparison_ShouldSendShrunkSourceToAi()
    {
        var aiRefiner = CreateAiRefiner();
        aiRefiner
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AiRefinementRequest>();
                var source = request.Comparisons.Single();
                source.OriginalText.ShouldBe("in");
                source.UserText.ShouldBe("and");

                return CreateRefinement(
                [
                    new AiRefinementDecision(
                        source.SourceComparisonIndex,
                        AiRefinementActions.Keep,
                        "source_range_already_minimal",
                        [])
                ]);
            });

        var service = CreateService(aiRefiner);
        var result = (await service.CompareTextsAsync(
            "in energy, healthcare, and",
            "and energy, health care, and",
            isPro: true,
            CancellationToken.None)).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.AiRefined);
        result.Comparisons.Single().OriginalText.ShouldBe("in");
        result.Comparisons.Single().UserText.ShouldBe("and");
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeTrue();
        result.Comparisons.Single().IsAiRefined.ShouldBeFalse();
    }

    [Fact]
    public async Task CompareTextsAsync_WhenAiFails_ShouldReturnDeterministicPreAiResult()
    {
        var aiRefiner = CreateAiRefiner();
        aiRefiner
            .RefineAsync(Arg.Any<AiRefinementRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiRefinementResult>>(_ => throw new InvalidOperationException("AI failed"));

        var service = CreateService(aiRefiner);
        var result = (await service.CompareTextsAsync(
            "in energy, healthcare, and",
            "and energy, health care, and",
            isPro: true,
            CancellationToken.None)).Result;

        result.CorrectionMode.ShouldBe(CorrectionModes.Fallback);
        result.Comparisons.Single().OriginalText.ShouldBe("in");
        result.Comparisons.Single().UserText.ShouldBe("and");
        result.Comparisons.Single().IsDeterministicallyRefined.ShouldBeTrue();
        result.Comparisons.Single().IsAiRefined.ShouldBeFalse();
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
        CreateRefinement(comparisons
            .GroupBy(comparison => comparison.SourceComparisonIndex)
            .Select(group => new AiRefinementDecision(
                group.Key,
                AiRefinementActions.Refine,
                "refined_genuine_error",
                group.ToList()))
            .ToList());

    private static AiRefinementResult CreateRefinement(
        IReadOnlyList<AiRefinementDecision> decisions) =>
        new(
            decisions,
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
            CreateDeterministicRefiner(),
            aiRefiner,
            new AiRefinementOutputValidator());
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

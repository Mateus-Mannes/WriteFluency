using Shouldly;
using Microsoft.Extensions.Options;
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
            userId: "user-1",
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
            userId: "user-1",
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
            userId: "user-1",
            CancellationToken.None)).Result;

        var comparison = result.Comparisons.Single();
        comparison.MistakePatternTags.ShouldBe(["spelling", "word_choice", "extra_word"]);
        comparison.MistakePatternPhrase.ShouldBe("Useful phrase.");
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUser_WhenUsageLimitIsReached_ShouldSkipClassifier()
    {
        var classifier = new RecordingMistakePatternClassifier(_ =>
        [
            new MistakePatternAnnotation(
                0,
                0,
                ["word_boundary"],
                "Word spacing changes the meaning here.")
        ]);
        var usageLimiter = new RecordingAiUsageLimiter(isAllowed: false);
        var service = CreateService(
            mistakePatternClassifier: classifier,
            aiUsageLimiter: usageLimiter);

        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            userId: "user-1",
            CancellationToken.None)).Result;

        classifier.CallCount.ShouldBe(0);
        usageLimiter.ReservationCount.ShouldBe(1);
        result.MistakePatternStatus.ShouldBe(MistakePatternStatuses.SkippedUsageLimit);
        result.MistakePatternMessage.ShouldNotBeNullOrWhiteSpace();
        result.Comparisons.ShouldAllBe(comparison =>
            comparison.MistakePatternTags == null
            && comparison.MistakePatternPhrase == null);
    }

    [Fact]
    public async Task CompareTextsAsync_ForAnonymousUserWithSampleCredit_ShouldRunFullProReview()
    {
        var classifier = CreateSingleAnnotationClassifier();
        var usageLimiter = new RecordingAiUsageLimiter(isAllowed: true);
        var service = CreateService(
            mistakePatternClassifier: classifier,
            aiUsageLimiter: usageLimiter);

        var result = (await service.CompareTextsAsync(
            new CorrectionOrchestrationRequest(
                "They may be ready",
                "They maybe ready",
                IsAuthenticated: false,
                IsPro: false,
                UserId: null,
                AnonymousFingerprintHash: "fingerprint-1",
                AnonymousClientIpAddress: "203.0.113.42",
                EnableFreeReviewTeaser: true),
            CancellationToken.None)).Result;

        classifier.CallCount.ShouldBe(1);
        usageLimiter.LastReservationRequest.ShouldNotBeNull();
        usageLimiter.LastReservationRequest.Feature.ShouldBe(
            AiUsageFeatures.MistakePatternClassificationAnonymousSample);
        usageLimiter.LastReservationRequest.UserId.ShouldBe("anonymous:fingerprint-1");
        usageLimiter.LastReservationRequest.AnonymousClientIpAddress.ShouldBe("203.0.113.42");
        result.CorrectionMode.ShouldBe(CorrectionModes.Normalized);
        result.MistakePatternStatus.ShouldBe(MistakePatternStatuses.Generated);
        result.MistakePatternReviewSource.ShouldBe(MistakePatternReviewSources.AnonymousSample);
        result.Comparisons.Single().MistakePatternTags.ShouldBe(["word_boundary"]);
    }

    [Fact]
    public async Task CompareTextsAsync_ForAnonymousUserAfterSampleCredit_ShouldReturnLoginRequiredAndSkipClassifier()
    {
        var classifier = CreateSingleAnnotationClassifier();
        var usageLimiter = new RecordingAiUsageLimiter(isAllowed: false);
        var service = CreateService(
            mistakePatternClassifier: classifier,
            aiUsageLimiter: usageLimiter);

        var result = (await service.CompareTextsAsync(
            new CorrectionOrchestrationRequest(
                "They may be ready",
                "They maybe ready",
                IsAuthenticated: false,
                IsPro: false,
                UserId: null,
                AnonymousFingerprintHash: "fingerprint-1",
                AnonymousClientIpAddress: "203.0.113.42",
                EnableFreeReviewTeaser: true),
            CancellationToken.None)).Result;

        classifier.CallCount.ShouldBe(0);
        result.MistakePatternStatus.ShouldBe(MistakePatternStatuses.LoginRequiredToUnlockReview);
        result.MistakePatternReviewSource.ShouldBe(MistakePatternReviewSources.None);
        result.MistakePatternMessage.ShouldNotBeNull();
        result.MistakePatternMessage.ShouldContain("Log in");
    }

    [Fact]
    public async Task CompareTextsAsync_ForLoggedInFreeUserWithIntroCredit_ShouldRunFullProReview()
    {
        var classifier = CreateSingleAnnotationClassifier();
        var usageLimiter = new RecordingAiUsageLimiter(isAllowed: true);
        var service = CreateService(
            mistakePatternClassifier: classifier,
            aiUsageLimiter: usageLimiter);

        var result = (await service.CompareTextsAsync(
            new CorrectionOrchestrationRequest(
                "They may be ready",
                "They maybe ready",
                IsAuthenticated: true,
                IsPro: false,
                UserId: "user-1",
                AnonymousFingerprintHash: null,
                AnonymousClientIpAddress: null,
                EnableFreeReviewTeaser: true),
            CancellationToken.None)).Result;

        classifier.CallCount.ShouldBe(1);
        usageLimiter.LastReservationRequest.ShouldNotBeNull();
        usageLimiter.LastReservationRequest.Feature.ShouldBe(
            AiUsageFeatures.MistakePatternClassificationFreeIntro);
        result.MistakePatternStatus.ShouldBe(MistakePatternStatuses.Generated);
        result.MistakePatternReviewSource.ShouldBe(MistakePatternReviewSources.FreeIntro);
    }

    [Fact]
    public async Task CompareTextsAsync_ForLoggedInFreeUserWithMonthlyCredit_ShouldUseMonthlyCreditAfterIntroIsUsed()
    {
        var classifier = CreateSingleAnnotationClassifier();
        var usageLimiter = new RecordingAiUsageLimiter(
            request => request.Feature == AiUsageFeatures.MistakePatternClassificationFreeMonthly);
        var service = CreateService(
            mistakePatternClassifier: classifier,
            aiUsageLimiter: usageLimiter);

        var result = (await service.CompareTextsAsync(
            new CorrectionOrchestrationRequest(
                "They may be ready",
                "They maybe ready",
                IsAuthenticated: true,
                IsPro: false,
                UserId: "user-1",
                AnonymousFingerprintHash: null,
                AnonymousClientIpAddress: null,
                EnableFreeReviewTeaser: true),
            CancellationToken.None)).Result;

        classifier.CallCount.ShouldBe(1);
        usageLimiter.ReservationRequests.Select(request => request.Feature).ShouldBe([
            AiUsageFeatures.MistakePatternClassificationFreeIntro,
            AiUsageFeatures.MistakePatternClassificationFreeMonthly
        ]);
        result.MistakePatternStatus.ShouldBe(MistakePatternStatuses.Generated);
        result.MistakePatternReviewSource.ShouldBe(MistakePatternReviewSources.FreeMonthly);
    }

    [Fact]
    public async Task CompareTextsAsync_ForLoggedInFreeUserWithoutCredit_ShouldReturnUpgradeRequiredAndSkipClassifier()
    {
        var classifier = CreateSingleAnnotationClassifier();
        var usageLimiter = new RecordingAiUsageLimiter(isAllowed: false);
        var service = CreateService(
            mistakePatternClassifier: classifier,
            aiUsageLimiter: usageLimiter);

        var result = (await service.CompareTextsAsync(
            new CorrectionOrchestrationRequest(
                "They may be ready",
                "They maybe ready",
                IsAuthenticated: true,
                IsPro: false,
                UserId: "user-1",
                AnonymousFingerprintHash: null,
                AnonymousClientIpAddress: null,
                EnableFreeReviewTeaser: true),
            CancellationToken.None)).Result;

        classifier.CallCount.ShouldBe(0);
        result.MistakePatternStatus.ShouldBe(MistakePatternStatuses.UpgradeRequiredToUnlockReview);
        result.MistakePatternReviewSource.ShouldBe(MistakePatternReviewSources.None);
        result.MistakePatternMessage.ShouldNotBeNull();
        result.MistakePatternMessage.ShouldContain("Upgrade");
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUser_WhenClassifierSucceeds_ShouldRecordUsageCompletion()
    {
        var usageLimiter = new RecordingAiUsageLimiter(isAllowed: true);
        var classifier = new RecordingMistakePatternClassifier(
            _ =>
            [
                new MistakePatternAnnotation(
                    0,
                    0,
                    ["word_boundary"],
                    "Word spacing changes the meaning here.")
            ],
            inputTokens: 123,
            outputTokens: 45);
        var service = CreateService(
            mistakePatternClassifier: classifier,
            aiUsageLimiter: usageLimiter);

        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            userId: "user-1",
            CancellationToken.None)).Result;

        result.MistakePatternStatus.ShouldBe(MistakePatternStatuses.Generated);
        result.MistakePatternReviewSource.ShouldBe(MistakePatternReviewSources.ProPaid);
        usageLimiter.CompletedCount.ShouldBe(1);
        usageLimiter.LastCompletion.ShouldNotBeNull();
        usageLimiter.LastCompletion.InputTokenCount.ShouldBe(123);
        usageLimiter.LastCompletion.OutputTokenCount.ShouldBe(45);
        usageLimiter.FailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task CompareTextsAsync_ForProUser_WhenClassifierFails_ShouldRecordUsageFailure()
    {
        var usageLimiter = new RecordingAiUsageLimiter(isAllowed: true);
        var service = CreateService(
            mistakePatternClassifier: new ThrowingMistakePatternClassifier(),
            aiUsageLimiter: usageLimiter);

        var result = (await service.CompareTextsAsync(
            "They may be ready",
            "They maybe ready",
            isPro: true,
            userId: "user-1",
            CancellationToken.None)).Result;

        result.MistakePatternStatus.ShouldBe(MistakePatternStatuses.ClassifierFailed);
        result.MistakePatternReviewSource.ShouldBe(MistakePatternReviewSources.None);
        result.MistakePatternMessage.ShouldNotBeNullOrWhiteSpace();
        usageLimiter.CompletedCount.ShouldBe(0);
        usageLimiter.FailedCount.ShouldBe(1);
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
        IMistakePatternClassifier? mistakePatternClassifier = null,
        IAiUsageLimiter? aiUsageLimiter = null)
    {
        var usageLimiter = aiUsageLimiter ?? new RecordingAiUsageLimiter(isAllowed: true);
        return new CorrectionOrchestrationService(
            CreateTextComparisonService(),
            CreateDeterministicRefiner(),
            mistakePatternClassifier ?? new EmptyMistakePatternClassifier(),
            usageLimiter,
            new ProReviewEligibilityService(
                usageLimiter,
                Options.Create(new ProReviewTeaserOptions())));
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

    private static RecordingMistakePatternClassifier CreateSingleAnnotationClassifier() =>
        new(_ =>
        [
            new MistakePatternAnnotation(
                0,
                0,
                ["word_boundary"],
                "Word spacing changes the meaning here.")
        ]);

    private sealed class RecordingMistakePatternClassifier : IMistakePatternClassifier
    {
        private readonly Func<MistakePatternClassificationRequest, IReadOnlyList<MistakePatternAnnotation>>
            _handler;
        private readonly long? _inputTokens;
        private readonly long? _outputTokens;

        public int CallCount { get; private set; }
        public bool IsEnabled => true;

        public RecordingMistakePatternClassifier(
            Func<MistakePatternClassificationRequest, IReadOnlyList<MistakePatternAnnotation>> handler,
            long? inputTokens = null,
            long? outputTokens = null)
        {
            _handler = handler;
            _inputTokens = inputTokens;
            _outputTokens = outputTokens;
        }

        public Task<MistakePatternClassificationRun> ClassifyWithDiagnosticsAsync(
            MistakePatternClassificationRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new MistakePatternClassificationRun(
                _handler(request),
                [
                    new MistakePatternClassificationRequestMetrics(
                        1,
                        0,
                        request.Comparisons.Count,
                        1,
                        _inputTokens,
                        _outputTokens,
                        (_inputTokens ?? 0) + (_outputTokens ?? 0))
                ]));
        }
    }

    private sealed class EmptyMistakePatternClassifier : IMistakePatternClassifier
    {
        public bool IsEnabled => true;

        public Task<MistakePatternClassificationRun> ClassifyWithDiagnosticsAsync(
            MistakePatternClassificationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MistakePatternClassificationRun([], []));
    }

    private sealed class ThrowingMistakePatternClassifier : IMistakePatternClassifier
    {
        public bool IsEnabled => true;

        public Task<MistakePatternClassificationRun> ClassifyWithDiagnosticsAsync(
            MistakePatternClassificationRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Classifier failed.");
    }

    private sealed class RecordingAiUsageLimiter : IAiUsageLimiter
    {
        private readonly Func<AiUsageReservationRequest, bool> _isAllowed;

        public int ReservationCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int FailedCount { get; private set; }
        public AiUsageCompletion? LastCompletion { get; private set; }
        public AiUsageReservationRequest? LastReservationRequest { get; private set; }
        public List<AiUsageReservationRequest> ReservationRequests { get; } = [];

        public RecordingAiUsageLimiter(bool isAllowed)
        {
            _isAllowed = _ => isAllowed;
        }

        public RecordingAiUsageLimiter(Func<AiUsageReservationRequest, bool> isAllowed)
        {
            _isAllowed = isAllowed;
        }

        public Task<AiUsageReservation> TryReserveAsync(
            AiUsageReservationRequest request,
            CancellationToken cancellationToken)
        {
            ReservationCount++;
            LastReservationRequest = request;
            ReservationRequests.Add(request);
            var reservation = _isAllowed(request)
                ? AiUsageReservation.Allowed(
                    request.UserId,
                    request.Feature,
                    "2026-07-06",
                    "2026-07")
                : AiUsageReservation.Denied(
                    "test_limit",
                    request.UserId,
                    request.Feature,
                    "2026-07-06",
                    "2026-07");

            return Task.FromResult(reservation);
        }

        public Task RecordCompletionAsync(
            AiUsageReservation reservation,
            AiUsageCompletion completion,
            CancellationToken cancellationToken)
        {
            CompletedCount++;
            LastCompletion = completion;
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(
            AiUsageReservation reservation,
            CancellationToken cancellationToken)
        {
            FailedCount++;
            return Task.CompletedTask;
        }
    }
}

using Microsoft.Extensions.Options;

namespace WriteFluency.TextComparisons;

public sealed class ProReviewEligibilityService
{
    private const string AnonymousSubjectPrefix = "anonymous:";

    private readonly IAiUsageLimiter _aiUsageLimiter;
    private readonly ProReviewTeaserOptions _options;

    public ProReviewEligibilityService(
        IAiUsageLimiter aiUsageLimiter,
        IOptions<ProReviewTeaserOptions> options)
    {
        _aiUsageLimiter = aiUsageLimiter;
        _options = options.Value;
    }

    public async Task<ProReviewEligibilityDecision> DecideAsync(
        CorrectionOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IsPro)
        {
            return await DecideProAsync(request, cancellationToken);
        }

        if (!request.EnableFreeReviewTeaser || !_options.Enabled)
        {
            return ProReviewEligibilityDecision.NoReview("free_teaser_disabled");
        }

        if (!request.IsAuthenticated)
        {
            return await DecideAnonymousAsync(request, cancellationToken);
        }

        return await DecideLoggedInFreeAsync(request, cancellationToken);
    }

    private async Task<ProReviewEligibilityDecision> DecideProAsync(
        CorrectionOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return ProReviewEligibilityDecision.UsageLimit(
                "The Pro AI review could not run because your session could not be verified.",
                "pro_missing_user_id");
        }

        var reservation = await _aiUsageLimiter.TryReserveAsync(
            new AiUsageReservationRequest(
                request.UserId,
                AiUsageFeatures.MistakePatternClassification),
            cancellationToken);

        return reservation.IsAllowed
            ? ProReviewEligibilityDecision.FullProReview(
                reservation,
                MistakePatternReviewSources.ProPaid,
                "pro_paid_quota_reserved")
            : ProReviewEligibilityDecision.UsageLimit(
                CreateUsageLimitMessage(reservation.DenialReason),
                reservation.DenialReason ?? "pro_paid_quota_denied");
    }

    private async Task<ProReviewEligibilityDecision> DecideAnonymousAsync(
        CorrectionOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AnonymousFingerprintHash))
        {
            return ProReviewEligibilityDecision.LockedLogin("anonymous_fingerprint_unavailable");
        }

        var reservation = await _aiUsageLimiter.TryReserveAsync(
            new AiUsageReservationRequest(
                AnonymousSubjectPrefix + request.AnonymousFingerprintHash,
                AiUsageFeatures.MistakePatternClassificationAnonymousSample,
                new AiUsageLimitPolicy(
                    LifetimeSubmissionLimit: _options.AnonymousSampleLifetimeLimit),
                request.AnonymousClientIpAddress),
            cancellationToken);

        return reservation.IsAllowed
            ? ProReviewEligibilityDecision.FullProReview(
                reservation,
                MistakePatternReviewSources.AnonymousSample,
                "anonymous_sample_reserved")
            : ProReviewEligibilityDecision.LockedLogin(
                reservation.DenialReason ?? "anonymous_sample_used");
    }

    private async Task<ProReviewEligibilityDecision> DecideLoggedInFreeAsync(
        CorrectionOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return ProReviewEligibilityDecision.LockedUpgrade("free_missing_user_id");
        }

        var introReservation = await _aiUsageLimiter.TryReserveAsync(
            new AiUsageReservationRequest(
                request.UserId,
                AiUsageFeatures.MistakePatternClassificationFreeIntro,
                new AiUsageLimitPolicy(
                    LifetimeSubmissionLimit: _options.FreeIntroLifetimeLimit)),
            cancellationToken);
        if (introReservation.IsAllowed)
        {
            return ProReviewEligibilityDecision.FullProReview(
                introReservation,
                MistakePatternReviewSources.FreeIntro,
                "free_intro_reserved");
        }

        var monthlyReservation = await _aiUsageLimiter.TryReserveAsync(
            new AiUsageReservationRequest(
                request.UserId,
                AiUsageFeatures.MistakePatternClassificationFreeMonthly,
                new AiUsageLimitPolicy(
                    MonthlySubmissionLimit: _options.FreeMonthlyLimit)),
            cancellationToken);

        return monthlyReservation.IsAllowed
            ? ProReviewEligibilityDecision.FullProReview(
                monthlyReservation,
                MistakePatternReviewSources.FreeMonthly,
                "free_monthly_reserved")
            : ProReviewEligibilityDecision.LockedUpgrade(
                monthlyReservation.DenialReason ?? "free_monthly_quota_used");
    }

    private static string CreateUsageLimitMessage(string? denialReason) =>
        denialReason switch
        {
            "daily_limit_exceeded" =>
                "You reached today's Pro AI review limit. Your correction highlights are still available; only the AI mistake-pattern review is paused. You can use AI review again tomorrow. If this seems unexpected, contact us on the Support page.",
            "monthly_limit_exceeded" =>
                "You reached this month's Pro AI review limit. Your correction highlights are still available; only the AI mistake-pattern review is paused. You can use AI review again when the monthly limit resets. If this seems unexpected, contact us on the Support page.",
            "monthly_cost_limit_exceeded" =>
                "Your Pro AI review is paused because this month's estimated AI usage limit was reached. This helps keep the Pro plan affordable. Your correction highlights are still available, and AI review will be available again when the monthly limit resets. If this seems unexpected, contact us on the Support page.",
            _ =>
                "Your Pro AI review limit was reached. Your correction highlights are still available; only the AI mistake-pattern review is paused. Please try again later, or contact us on the Support page if this seems unexpected."
        };
}

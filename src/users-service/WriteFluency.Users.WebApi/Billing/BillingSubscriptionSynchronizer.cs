using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Billing;

public sealed class BillingSubscriptionSynchronizer
{
    public void Apply(
        ApplicationUser user,
        StripeSubscriptionResult subscription,
        DateTimeOffset nowUtc)
    {
        var accessEndUtc = subscription.CancelAtUtc ?? subscription.CurrentPeriodEndUtc;

        if (!string.IsNullOrWhiteSpace(subscription.CustomerId))
        {
            user.StripeCustomerId = subscription.CustomerId;
        }

        user.StripeSubscriptionId = subscription.Id;
        user.StripeSubscriptionStatus = subscription.Status;
        user.SubscriptionCurrentPeriodEndUtc = accessEndUtc;
        user.SubscriptionCancelAtPeriodEnd = IsSubscriptionCanceling(subscription, nowUtc);
        user.SubscriptionPlan = IsProEligibleSubscription(subscription, accessEndUtc, nowUtc)
            ? SubscriptionEntitlements.ProPlan
            : SubscriptionEntitlements.FreePlan;
    }

    private static bool IsProEligibleSubscription(
        StripeSubscriptionResult subscription,
        DateTimeOffset? accessEndUtc,
        DateTimeOffset nowUtc)
    {
        return IsPaidPeriodEligibleStatus(subscription.Status)
            && accessEndUtc > nowUtc;
    }

    private static bool IsPaidPeriodEligibleStatus(string? status)
    {
        return string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "past_due", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubscriptionCanceling(StripeSubscriptionResult subscription, DateTimeOffset nowUtc)
    {
        return subscription.CancelAtPeriodEnd
            || subscription.CancelAtUtc > nowUtc;
    }
}

using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Billing;

public static class SubscriptionEntitlements
{
    public const string FreePlan = "free";
    public const string ProPlan = "pro";
    public const string FreeStatus = "free";
    public const string ProActiveStatus = "pro_active";
    public const string ProCancelingStatus = "pro_canceling";
    public const string ProExpiredStatus = "pro_expired";

    public static SubscriptionEntitlement Build(ApplicationUser? user, DateTimeOffset nowUtc)
    {
        var plan = string.Equals(user?.SubscriptionPlan, ProPlan, StringComparison.OrdinalIgnoreCase)
            ? ProPlan
            : FreePlan;
        var currentPeriodEndUtc = user?.SubscriptionCurrentPeriodEndUtc;
        var cancelAtPeriodEnd = user?.SubscriptionCancelAtPeriodEnd ?? false;

        if (plan is not ProPlan)
        {
            return new SubscriptionEntitlement(
                Plan: FreePlan,
                Status: FreeStatus,
                IsPro: false,
                CurrentPeriodEndUtc: currentPeriodEndUtc,
                CancelAtPeriodEnd: cancelAtPeriodEnd);
        }

        if (currentPeriodEndUtc is null || currentPeriodEndUtc <= nowUtc)
        {
            return new SubscriptionEntitlement(
                Plan: ProPlan,
                Status: ProExpiredStatus,
                IsPro: false,
                CurrentPeriodEndUtc: currentPeriodEndUtc,
                CancelAtPeriodEnd: cancelAtPeriodEnd);
        }

        return new SubscriptionEntitlement(
            Plan: ProPlan,
            Status: cancelAtPeriodEnd ? ProCancelingStatus : ProActiveStatus,
            IsPro: true,
            CurrentPeriodEndUtc: currentPeriodEndUtc,
            CancelAtPeriodEnd: cancelAtPeriodEnd);
    }
}

public sealed record SubscriptionEntitlement(
    string Plan,
    string Status,
    bool IsPro,
    DateTimeOffset? CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd);

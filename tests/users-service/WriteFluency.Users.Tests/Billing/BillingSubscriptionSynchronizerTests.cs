using Shouldly;
using WriteFluency.Users.WebApi.Billing;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.Tests.Billing;

public class BillingSubscriptionSynchronizerTests
{
    private static readonly DateTimeOffset NowUtc = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FutureUtc = NowUtc.AddMonths(1);

    [Theory]
    [InlineData("active", "pro")]
    [InlineData("past_due", "pro")]
    [InlineData("canceled", "free")]
    [InlineData("unpaid", "free")]
    [InlineData("incomplete_expired", "free")]
    public void Apply_ShouldMapStripeStatusToExpectedPlan(string stripeStatus, string expectedPlan)
    {
        var user = new ApplicationUser();
        var subscription = CreateSubscription(stripeStatus, FutureUtc);

        new BillingSubscriptionSynchronizer().Apply(user, subscription, NowUtc);

        user.SubscriptionPlan.ShouldBe(expectedPlan);
        user.StripeCustomerId.ShouldBe("cus_test");
        user.StripeSubscriptionId.ShouldBe("sub_test");
        user.StripeSubscriptionStatus.ShouldBe(stripeStatus);
        user.SubscriptionCurrentPeriodEndUtc.ShouldBe(FutureUtc);
    }

    [Fact]
    public void Apply_WhenPaidPeriodEnded_ShouldRemovePro()
    {
        var user = new ApplicationUser();
        var subscription = CreateSubscription("active", NowUtc.AddSeconds(-1));

        new BillingSubscriptionSynchronizer().Apply(user, subscription, NowUtc);

        user.SubscriptionPlan.ShouldBe(SubscriptionEntitlements.FreePlan);
    }

    [Fact]
    public void Apply_WhenFutureCancelAtExists_ShouldUseItAsAccessEndAndMarkCancellation()
    {
        var user = new ApplicationUser();
        var cancelAtUtc = NowUtc.AddDays(10);
        var subscription = CreateSubscription("active", FutureUtc) with
        {
            CancelAtUtc = cancelAtUtc
        };

        new BillingSubscriptionSynchronizer().Apply(user, subscription, NowUtc);

        user.SubscriptionPlan.ShouldBe(SubscriptionEntitlements.ProPlan);
        user.SubscriptionCurrentPeriodEndUtc.ShouldBe(cancelAtUtc);
        user.SubscriptionCancelAtPeriodEnd.ShouldBeTrue();
    }

    private static StripeSubscriptionResult CreateSubscription(string status, DateTimeOffset currentPeriodEndUtc)
    {
        return new StripeSubscriptionResult(
            Id: "sub_test",
            CustomerId: "cus_test",
            Status: status,
            CurrentPeriodEndUtc: currentPeriodEndUtc,
            CancelAtUtc: null,
            CancelAtPeriodEnd: false);
    }
}

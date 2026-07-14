using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Stripe;
using WriteFluency.Users.IntegrationTests.Infrastructure;
using WriteFluency.Users.WebApi.Billing;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.IntegrationTests.Billing;

public class BillingEndpointsIntegrationTests : IClassFixture<UsersApiIntegrationFixture>
{
    private const string WebhookSecret = "whsec_test_writefluency";

    private readonly UsersApiIntegrationFixture _fixture;

    public BillingEndpointsIntegrationTests(UsersApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CheckoutSession_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/checkout-session", new { });

        IsUnauthenticatedStatus(response.StatusCode).ShouldBeTrue();
    }

    [Fact]
    public async Task CheckoutSession_ForFreeUser_ShouldCreateStripeCustomerAndCheckoutSession()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-free-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/checkout-session", new { });

        response.IsSuccessStatusCode.ShouldBeTrue();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("status").GetString().ShouldBe("checkout_created");
        doc.RootElement.GetProperty("checkoutUrl").GetString().ShouldBe("https://checkout.stripe.test/session/1");

        _fixture.StripeBillingClient.CreatedCustomers.Count.ShouldBe(1);
        _fixture.StripeBillingClient.CreatedCustomers[0].Email.ShouldBe(email);
        _fixture.StripeBillingClient.CreatedCheckoutSessions.Count.ShouldBe(1);
        var checkoutRequest = _fixture.StripeBillingClient.CreatedCheckoutSessions[0];
        checkoutRequest.ProMonthlyPriceId.ShouldBe("price_test_pro_monthly");
        checkoutRequest.SuccessUrl.ShouldBe("http://localhost:4200/user?checkout=success&session_id={CHECKOUT_SESSION_ID}");
        checkoutRequest.CancelUrl.ShouldBe("http://localhost:4200/user?checkout=cancelled");

        var user = await GetUserByEmailAsync(email);
        user.StripeCustomerId.ShouldBe(_fixture.StripeBillingClient.CreatedCustomers[0].CustomerId);
    }

    [Fact]
    public async Task CheckoutSession_WhenStripeCustomerExists_ShouldReuseCustomer()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-existing-customer-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetStripeCustomerAsync(email, "cus_existing");

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/checkout-session", new { });

        response.IsSuccessStatusCode.ShouldBeTrue();
        _fixture.StripeBillingClient.CreatedCustomers.Count.ShouldBe(0);
        _fixture.StripeBillingClient.CreatedCheckoutSessions.Count.ShouldBe(1);
        _fixture.StripeBillingClient.CreatedCheckoutSessions[0].CustomerId.ShouldBe("cus_existing");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckoutSession_ForActiveOrCancelingProUser_ShouldNotCreateDuplicateCheckout(bool cancelAtPeriodEnd)
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-pro-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetSubscriptionEntitlementAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            cancelAtPeriodEnd);

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/checkout-session", new { });

        response.IsSuccessStatusCode.ShouldBeTrue();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("status").GetString().ShouldBe("subscription_management_required");
        doc.RootElement.GetProperty("checkoutUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        _fixture.StripeBillingClient.CreatedCustomers.Count.ShouldBe(0);
        _fixture.StripeBillingClient.CreatedCheckoutSessions.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-session")]
    public async Task ConfirmCheckoutSession_WhenSessionIdIsMissingOrInvalid_ShouldReturnBadRequest(string sessionId)
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-invalid-confirm-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetStripeCustomerAsync(email, "cus_current");

        var response = await PostAsJsonWithAllowedOriginAsync(
            client,
            "/users/billing/checkout-session/confirm",
            new { SessionId = sessionId });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConfirmCheckoutSession_WhenSessionBelongsToAnotherCustomer_ShouldReturnForbidden()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-wrong-customer-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        var user = await SetStripeCustomerAsync(email, "cus_current");
        _fixture.StripeBillingClient.CheckoutSessions["cs_wrong_customer"] = new StripeCheckoutSessionResult(
            Id: "cs_wrong_customer",
            CustomerId: "cus_other",
            SubscriptionId: "sub_confirm",
            Url: null,
            Mode: "subscription",
            ClientReferenceId: user.Id,
            Status: "complete");

        var response = await PostAsJsonWithAllowedOriginAsync(
            client,
            "/users/billing/checkout-session/confirm",
            new { SessionId = "cs_wrong_customer" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConfirmCheckoutSession_WhenSubscriptionIsActive_ShouldPersistProEntitlement()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-confirm-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        var user = await SetStripeCustomerAsync(email, "cus_confirm");
        var currentPeriodEndUtc = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        _fixture.StripeBillingClient.CheckoutSessions["cs_confirm"] = new StripeCheckoutSessionResult(
            Id: "cs_confirm",
            CustomerId: "cus_confirm",
            SubscriptionId: "sub_confirm",
            Url: null,
            Mode: "subscription",
            ClientReferenceId: user.Id,
            Status: "complete");
        _fixture.StripeBillingClient.Subscriptions["sub_confirm"] = new StripeSubscriptionResult(
            Id: "sub_confirm",
            CustomerId: "cus_confirm",
            Status: "active",
            CurrentPeriodEndUtc: currentPeriodEndUtc,
            CancelAtUtc: null,
            CancelAtPeriodEnd: false);

        var response = await PostAsJsonWithAllowedOriginAsync(
            client,
            "/users/billing/checkout-session/confirm",
            new { SessionId = "cs_confirm" });

        response.IsSuccessStatusCode.ShouldBeTrue();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("plan").GetString().ShouldBe("pro");
        doc.RootElement.GetProperty("entitlementStatus").GetString().ShouldBe("pro_active");
        doc.RootElement.GetProperty("isPro").GetBoolean().ShouldBeTrue();

        var updatedUser = await GetUserByEmailAsync(email);
        updatedUser.StripeCustomerId.ShouldBe("cus_confirm");
        updatedUser.StripeSubscriptionId.ShouldBe("sub_confirm");
        updatedUser.StripeSubscriptionStatus.ShouldBe("active");
        updatedUser.SubscriptionPlan.ShouldBe("pro");
        updatedUser.SubscriptionCurrentPeriodEndUtc.ShouldBe(currentPeriodEndUtc);
        updatedUser.SubscriptionCancelAtPeriodEnd.ShouldBeFalse();
    }

    [Fact]
    public async Task PortalSession_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/portal-session", new { });

        IsUnauthenticatedStatus(response.StatusCode).ShouldBeTrue();
    }

    [Theory]
    [InlineData(SubscriptionEntitlements.FreePlan, null, false, "free_user")]
    [InlineData(SubscriptionEntitlements.ProPlan, "2020-01-01T00:00:00+00:00", false, "pro_expired")]
    public async Task PortalSession_ForFreeOrExpiredUser_ShouldReturnForbidden(
        string plan,
        string? periodEnd,
        bool cancelAtPeriodEnd,
        string expectedReason)
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-portal-forbidden-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetSubscriptionStateAsync(
            email,
            plan,
            periodEnd is null ? null : DateTimeOffset.Parse(periodEnd),
            cancelAtPeriodEnd,
            "cus_portal",
            "sub_portal",
            "active");

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/portal-session", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("error").GetString().ShouldBe("subscription_management_unavailable");
        doc.RootElement.GetProperty("reason").GetString().ShouldBe(expectedReason);
    }

    [Fact]
    public async Task PortalSession_WhenBillingReferencesAreMissing_ShouldReturnConflict()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-portal-missing-refs-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetSubscriptionEntitlementAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            false);

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/portal-session", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("error").GetString().ShouldBe("missing_stripe_billing_reference");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortalSession_ForActiveOrCancelingProUser_ShouldCreateStripePortalSession(bool cancelAtPeriodEnd)
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-portal-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            cancelAtPeriodEnd,
            "cus_portal",
            "sub_portal",
            "active");

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/portal-session", new { });

        response.IsSuccessStatusCode.ShouldBeTrue();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("portalUrl").GetString().ShouldBe("https://billing.stripe.test/session/1");
        _fixture.StripeBillingClient.CreatedPortalSessions.Count.ShouldBe(1);
        var portalRequest = _fixture.StripeBillingClient.CreatedPortalSessions[0];
        portalRequest.CustomerId.ShouldBe("cus_portal");
        portalRequest.PortalConfigurationId.ShouldBe("bpc_test_writefluency");
        portalRequest.ReturnUrl.ShouldBe("http://localhost:4200/user?billing=returned");
    }

    [Fact]
    public async Task SyncSubscription_WhenBillingReferencesAreMissing_ShouldReturnConflict()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-sync-missing-refs-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/sync", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SyncSubscription_WhenSubscriptionIsCanceling_ShouldPersistCancelingEntitlement()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-sync-canceling-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            false,
            "cus_sync",
            "sub_sync",
            "active");
        var periodEndUtc = new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero);
        _fixture.StripeBillingClient.Subscriptions["sub_sync"] = new StripeSubscriptionResult(
            Id: "sub_sync",
            CustomerId: "cus_sync",
            Status: "active",
            CurrentPeriodEndUtc: periodEndUtc,
            CancelAtUtc: null,
            CancelAtPeriodEnd: true);

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/sync", new { });

        response.IsSuccessStatusCode.ShouldBeTrue();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("plan").GetString().ShouldBe("pro");
        doc.RootElement.GetProperty("entitlementStatus").GetString().ShouldBe("pro_canceling");
        doc.RootElement.GetProperty("isPro").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("cancelAtPeriodEnd").GetBoolean().ShouldBeTrue();

        var updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionPlan.ShouldBe("pro");
        updatedUser.StripeSubscriptionStatus.ShouldBe("active");
        updatedUser.SubscriptionCurrentPeriodEndUtc.ShouldBe(periodEndUtc);
        updatedUser.SubscriptionCancelAtPeriodEnd.ShouldBeTrue();
    }

    [Fact]
    public async Task SyncSubscription_WhenSubscriptionHasFutureCancelAt_ShouldPersistCancelingEntitlement()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-sync-cancel-at-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            false,
            "cus_sync_cancel_at",
            "sub_sync_cancel_at",
            "active");
        var periodEndUtc = new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var cancelAtUtc = new DateTimeOffset(2030, 1, 15, 0, 0, 0, TimeSpan.Zero);
        _fixture.StripeBillingClient.Subscriptions["sub_sync_cancel_at"] = new StripeSubscriptionResult(
            Id: "sub_sync_cancel_at",
            CustomerId: "cus_sync_cancel_at",
            Status: "active",
            CurrentPeriodEndUtc: periodEndUtc,
            CancelAtUtc: cancelAtUtc,
            CancelAtPeriodEnd: false);

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/sync", new { });

        response.IsSuccessStatusCode.ShouldBeTrue();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("plan").GetString().ShouldBe("pro");
        doc.RootElement.GetProperty("entitlementStatus").GetString().ShouldBe("pro_canceling");
        doc.RootElement.GetProperty("isPro").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("cancelAtPeriodEnd").GetBoolean().ShouldBeTrue();

        var updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionPlan.ShouldBe("pro");
        updatedUser.StripeSubscriptionStatus.ShouldBe("active");
        updatedUser.SubscriptionCurrentPeriodEndUtc.ShouldBe(cancelAtUtc);
        updatedUser.SubscriptionCancelAtPeriodEnd.ShouldBeTrue();
    }

    [Theory]
    [InlineData("canceled")]
    [InlineData("incomplete_expired")]
    public async Task SyncSubscription_WhenSubscriptionIsNoLongerEligible_ShouldPersistFreeEntitlement(string stripeStatus)
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-sync-expired-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            false,
            "cus_sync_expired",
            "sub_sync_expired",
            "active");
        _fixture.StripeBillingClient.Subscriptions["sub_sync_expired"] = new StripeSubscriptionResult(
            Id: "sub_sync_expired",
            CustomerId: "cus_sync_expired",
            Status: stripeStatus,
            CurrentPeriodEndUtc: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CancelAtUtc: null,
            CancelAtPeriodEnd: false);

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/sync", new { });

        response.IsSuccessStatusCode.ShouldBeTrue();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("plan").GetString().ShouldBe("free");
        doc.RootElement.GetProperty("entitlementStatus").GetString().ShouldBe("free");
        doc.RootElement.GetProperty("isPro").GetBoolean().ShouldBeFalse();

        var updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionPlan.ShouldBe("free");
        updatedUser.StripeSubscriptionStatus.ShouldBe(stripeStatus);
        updatedUser.SubscriptionCancelAtPeriodEnd.ShouldBeFalse();
    }

    [Fact]
    public async Task SyncSubscription_WhenSubscriptionBelongsToAnotherCustomer_ShouldReturnForbidden()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-sync-wrong-customer-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            false,
            "cus_current",
            "sub_current",
            "active");
        _fixture.StripeBillingClient.Subscriptions["sub_current"] = new StripeSubscriptionResult(
            Id: "sub_current",
            CustomerId: "cus_other",
            Status: "active",
            CurrentPeriodEndUtc: new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CancelAtUtc: null,
            CancelAtPeriodEnd: false);

        var response = await PostAsJsonWithAllowedOriginAsync(client, "/users/billing/sync", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StripeWebhook_WhenSignatureIsMissingOrInvalid_ShouldReturnBadRequestWithoutCsrfOrigin()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();
        var payload = CreateStripeEventPayload($"evt_{Guid.NewGuid():N}", "product.created", new { id = "prod_test" });

        using var missingSignature = new HttpRequestMessage(HttpMethod.Post, "/users/billing/stripe-webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        var missingSignatureResponse = await client.SendAsync(missingSignature);
        missingSignatureResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var invalidSignature = new HttpRequestMessage(HttpMethod.Post, "/users/billing/stripe-webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        invalidSignature.Headers.TryAddWithoutValidation("Stripe-Signature", "t=1,v1=invalid");
        var invalidSignatureResponse = await client.SendAsync(invalidSignature);
        invalidSignatureResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StripeWebhook_WhenCheckoutCompletes_ShouldActivateProAndIgnoreDuplicateDelivery()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-webhook-checkout-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        var user = await SetStripeCustomerAsync(email, "cus_webhook_checkout");
        var periodEndUtc = new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero);
        _fixture.StripeBillingClient.CheckoutSessions["cs_webhook_checkout"] = new StripeCheckoutSessionResult(
            Id: "cs_webhook_checkout",
            CustomerId: "cus_webhook_checkout",
            SubscriptionId: "sub_webhook_checkout",
            Url: null,
            Mode: "subscription",
            ClientReferenceId: user.Id,
            Status: "complete");
        _fixture.StripeBillingClient.Subscriptions["sub_webhook_checkout"] = new StripeSubscriptionResult(
            Id: "sub_webhook_checkout",
            CustomerId: "cus_webhook_checkout",
            Status: "active",
            CurrentPeriodEndUtc: periodEndUtc,
            CancelAtUtc: null,
            CancelAtPeriodEnd: false,
            WriteFluencyUserId: user.Id);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = CreateStripeEventPayload(
            eventId,
            EventTypes.CheckoutSessionCompleted,
            new { id = "cs_webhook_checkout" });

        var response = await PostStripeWebhookAsync(client, payload);

        response.IsSuccessStatusCode.ShouldBeTrue();
        var updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionPlan.ShouldBe(SubscriptionEntitlements.ProPlan);
        updatedUser.StripeSubscriptionId.ShouldBe("sub_webhook_checkout");
        updatedUser.SubscriptionCurrentPeriodEndUtc.ShouldBe(periodEndUtc);
        (await GetWebhookEventAsync(eventId)).ProcessingStatus.ShouldBe(StripeWebhookEventStatuses.Processed);

        _fixture.StripeBillingClient.Subscriptions["sub_webhook_checkout"] =
            _fixture.StripeBillingClient.Subscriptions["sub_webhook_checkout"] with
            {
                Status = "canceled",
                CurrentPeriodEndUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
            };

        var duplicateResponse = await PostStripeWebhookAsync(client, payload);

        duplicateResponse.IsSuccessStatusCode.ShouldBeTrue();
        updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionPlan.ShouldBe(SubscriptionEntitlements.ProPlan);
        (await GetWebhookEventAsync(eventId)).AttemptCount.ShouldBe(1);

        _fixture.StripeBillingClient.CheckoutSessions["cs_webhook_checkout_old"] = new StripeCheckoutSessionResult(
            Id: "cs_webhook_checkout_old",
            CustomerId: "cus_webhook_checkout",
            SubscriptionId: "sub_webhook_checkout_old",
            Url: null,
            Mode: "subscription",
            ClientReferenceId: user.Id,
            Status: "complete");
        _fixture.StripeBillingClient.Subscriptions["sub_webhook_checkout_old"] = new StripeSubscriptionResult(
            Id: "sub_webhook_checkout_old",
            CustomerId: "cus_webhook_checkout",
            Status: "active",
            CurrentPeriodEndUtc: periodEndUtc.AddMonths(1),
            CancelAtUtc: null,
            CancelAtPeriodEnd: false,
            WriteFluencyUserId: user.Id);
        var oldCheckoutEventId = $"evt_{Guid.NewGuid():N}";

        var oldCheckoutResponse = await PostStripeWebhookAsync(
            client,
            CreateStripeEventPayload(
                oldCheckoutEventId,
                EventTypes.CheckoutSessionCompleted,
                new { id = "cs_webhook_checkout_old" },
                DateTimeOffset.UtcNow.AddDays(-1)));

        oldCheckoutResponse.IsSuccessStatusCode.ShouldBeTrue();
        updatedUser = await GetUserByEmailAsync(email);
        updatedUser.StripeSubscriptionId.ShouldBe("sub_webhook_checkout");
        (await GetWebhookEventAsync(oldCheckoutEventId)).ProcessingStatus.ShouldBe(StripeWebhookEventStatuses.Ignored);

        using var sessionDocument = JsonDocument.Parse(await client.GetStringAsync("/users/auth/session"));
        sessionDocument.RootElement.GetProperty("isPro").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task StripeWebhook_WhenSubscriptionUpdateSchedulesCancellation_ShouldKeepProAndIgnoreOlderSubscription()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-webhook-canceling-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        var user = await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            false,
            "cus_webhook_canceling",
            "sub_webhook_current",
            "active");
        var periodEndUtc = new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero);
        _fixture.StripeBillingClient.Subscriptions["sub_webhook_current"] = new StripeSubscriptionResult(
            Id: "sub_webhook_current",
            CustomerId: "cus_webhook_canceling",
            Status: "active",
            CurrentPeriodEndUtc: periodEndUtc,
            CancelAtUtc: null,
            CancelAtPeriodEnd: true,
            WriteFluencyUserId: user.Id);

        var response = await PostStripeWebhookAsync(
            client,
            CreateStripeEventPayload(
                $"evt_{Guid.NewGuid():N}",
                EventTypes.CustomerSubscriptionUpdated,
                new { id = "sub_webhook_current" }));

        response.IsSuccessStatusCode.ShouldBeTrue();
        var updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionPlan.ShouldBe(SubscriptionEntitlements.ProPlan);
        updatedUser.SubscriptionCancelAtPeriodEnd.ShouldBeTrue();
        updatedUser.SubscriptionCurrentPeriodEndUtc.ShouldBe(periodEndUtc);

        _fixture.StripeBillingClient.Subscriptions["sub_webhook_old"] = new StripeSubscriptionResult(
            Id: "sub_webhook_old",
            CustomerId: "cus_webhook_canceling",
            Status: "canceled",
            CurrentPeriodEndUtc: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CancelAtUtc: null,
            CancelAtPeriodEnd: false,
            WriteFluencyUserId: user.Id);
        var staleEventId = $"evt_{Guid.NewGuid():N}";

        var staleResponse = await PostStripeWebhookAsync(
            client,
            CreateStripeEventPayload(
                staleEventId,
                EventTypes.CustomerSubscriptionUpdated,
                new { id = "sub_webhook_old" }));

        staleResponse.IsSuccessStatusCode.ShouldBeTrue();
        updatedUser = await GetUserByEmailAsync(email);
        updatedUser.StripeSubscriptionId.ShouldBe("sub_webhook_current");
        updatedUser.SubscriptionPlan.ShouldBe(SubscriptionEntitlements.ProPlan);
        (await GetWebhookEventAsync(staleEventId)).ProcessingStatus.ShouldBe(StripeWebhookEventStatuses.Ignored);
    }

    [Fact]
    public async Task StripeWebhook_WhenSubscriptionIsDeleted_ShouldRemovePro()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-webhook-deleted-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        var user = await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            true,
            "cus_webhook_deleted",
            "sub_webhook_deleted",
            "active");

        var response = await PostStripeWebhookAsync(
            client,
            CreateStripeEventPayload(
                $"evt_{Guid.NewGuid():N}",
                EventTypes.CustomerSubscriptionDeleted,
                CreateSubscriptionPayload(
                    "sub_webhook_deleted",
                    "cus_webhook_deleted",
                    "canceled",
                    new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    user.Id)));

        response.IsSuccessStatusCode.ShouldBeTrue();
        var updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionPlan.ShouldBe(SubscriptionEntitlements.FreePlan);
        updatedUser.StripeSubscriptionStatus.ShouldBe("canceled");
        updatedUser.SubscriptionCancelAtPeriodEnd.ShouldBeFalse();
    }

    [Fact]
    public async Task StripeWebhook_WhenInvoiceIsPaidOrPaymentFails_ShouldSyncLatestPaidPeriod()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-webhook-invoice-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        var user = await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            false,
            "cus_webhook_invoice",
            "sub_webhook_invoice",
            "active");
        var renewedPeriodEndUtc = new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero);
        _fixture.StripeBillingClient.Subscriptions["sub_webhook_invoice"] = new StripeSubscriptionResult(
            Id: "sub_webhook_invoice",
            CustomerId: "cus_webhook_invoice",
            Status: "active",
            CurrentPeriodEndUtc: renewedPeriodEndUtc,
            CancelAtUtc: null,
            CancelAtPeriodEnd: false,
            WriteFluencyUserId: user.Id);

        var paidResponse = await PostStripeWebhookAsync(
            client,
            CreateStripeEventPayload(
                $"evt_{Guid.NewGuid():N}",
                EventTypes.InvoicePaid,
                CreateInvoicePayload("sub_webhook_invoice")));

        paidResponse.IsSuccessStatusCode.ShouldBeTrue();
        var updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionCurrentPeriodEndUtc.ShouldBe(renewedPeriodEndUtc);

        _fixture.StripeBillingClient.Subscriptions["sub_webhook_invoice"] =
            _fixture.StripeBillingClient.Subscriptions["sub_webhook_invoice"] with { Status = "past_due" };
        var failedResponse = await PostStripeWebhookAsync(
            client,
            CreateStripeEventPayload(
                $"evt_{Guid.NewGuid():N}",
                EventTypes.InvoicePaymentFailed,
                CreateInvoicePayload("sub_webhook_invoice")));

        failedResponse.IsSuccessStatusCode.ShouldBeTrue();
        updatedUser = await GetUserByEmailAsync(email);
        updatedUser.SubscriptionPlan.ShouldBe(SubscriptionEntitlements.ProPlan);
        updatedUser.StripeSubscriptionStatus.ShouldBe("past_due");
        updatedUser.SubscriptionCurrentPeriodEndUtc.ShouldBe(renewedPeriodEndUtc);
    }

    [Fact]
    public async Task StripeWebhook_WhenProcessingFails_ShouldRecordFailureAndAllowRetry()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"billing-webhook-retry-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        var user = await SetSubscriptionStateAsync(
            email,
            SubscriptionEntitlements.ProPlan,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            false,
            "cus_webhook_retry",
            "sub_webhook_retry",
            "active");
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = CreateStripeEventPayload(
            eventId,
            EventTypes.CustomerSubscriptionUpdated,
            new { id = "sub_webhook_retry" });

        var failedResponse = await PostStripeWebhookAsync(client, payload);

        failedResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var webhookEvent = await GetWebhookEventAsync(eventId);
        webhookEvent.ProcessingStatus.ShouldBe(StripeWebhookEventStatuses.Failed);
        webhookEvent.AttemptCount.ShouldBe(1);

        _fixture.StripeBillingClient.Subscriptions["sub_webhook_retry"] = new StripeSubscriptionResult(
            Id: "sub_webhook_retry",
            CustomerId: "cus_webhook_retry",
            Status: "active",
            CurrentPeriodEndUtc: new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero),
            CancelAtUtc: null,
            CancelAtPeriodEnd: false,
            WriteFluencyUserId: user.Id);

        var retryResponse = await PostStripeWebhookAsync(client, payload);

        retryResponse.IsSuccessStatusCode.ShouldBeTrue();
        webhookEvent = await GetWebhookEventAsync(eventId);
        webhookEvent.ProcessingStatus.ShouldBe(StripeWebhookEventStatuses.Processed);
        webhookEvent.AttemptCount.ShouldBe(2);
    }

    [Fact]
    public async Task StripeWebhook_WhenStripeReferencesResolveToDifferentUsers_ShouldFailForRetry()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        const string password = "Passw0rd!123";
        var metadataEmail = $"billing-webhook-conflict-metadata-{Guid.NewGuid():N}@writefluency.test";
        await RegisterConfirmAndLoginAsync(client, metadataEmail, password);
        var metadataUser = await SetStripeCustomerAsync(metadataEmail, "cus_webhook_metadata");

        var customerEmail = $"billing-webhook-conflict-customer-{Guid.NewGuid():N}@writefluency.test";
        await RegisterConfirmAndLoginAsync(client, customerEmail, password);
        await SetStripeCustomerAsync(customerEmail, "cus_webhook_conflict");

        _fixture.StripeBillingClient.Subscriptions["sub_webhook_conflict"] = new StripeSubscriptionResult(
            Id: "sub_webhook_conflict",
            CustomerId: "cus_webhook_conflict",
            Status: "active",
            CurrentPeriodEndUtc: new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero),
            CancelAtUtc: null,
            CancelAtPeriodEnd: false,
            WriteFluencyUserId: metadataUser.Id);
        var eventId = $"evt_{Guid.NewGuid():N}";

        var response = await PostStripeWebhookAsync(
            client,
            CreateStripeEventPayload(
                eventId,
                EventTypes.CustomerSubscriptionUpdated,
                new { id = "sub_webhook_conflict" }));

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var webhookEvent = await GetWebhookEventAsync(eventId);
        webhookEvent.ProcessingStatus.ShouldBe(StripeWebhookEventStatuses.Failed);
        webhookEvent.LastError.ShouldNotBeNull();
        webhookEvent.LastError!.ShouldContain("different WriteFluency users");
    }

    [Fact]
    public async Task StripeWebhook_WhenEventIsUnrelated_ShouldMarkItIgnored()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();
        var eventId = $"evt_{Guid.NewGuid():N}";

        var response = await PostStripeWebhookAsync(
            client,
            CreateStripeEventPayload(eventId, "product.created", new { id = "prod_test" }));

        response.IsSuccessStatusCode.ShouldBeTrue();
        (await GetWebhookEventAsync(eventId)).ProcessingStatus.ShouldBe(StripeWebhookEventStatuses.Ignored);
    }

    private bool CanRunIntegration()
    {
        return _fixture.IsAvailable;
    }

    private async Task RegisterConfirmAndLoginAsync(HttpClient client, string email, string password)
    {
        var register = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/register", new
        {
            Email = email,
            Password = password
        });
        register.IsSuccessStatusCode.ShouldBeTrue();

        var confirmationEmail = _fixture.EmailSender.FindLastBySubjectContains("Confirm your WriteFluency email");
        confirmationEmail.ShouldNotBeNull();

        var confirmUrl = BuildUsersConfirmEmailUrlFromWebappLink(confirmationEmail!.HtmlBody);
        var confirm = await client.GetAsync(confirmUrl);
        confirm.IsSuccessStatusCode.ShouldBeTrue();

        var login = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/login?useCookies=true", new
        {
            Email = email,
            Password = password
        });
        login.IsSuccessStatusCode.ShouldBeTrue();
    }

    private async Task<ApplicationUser> SetStripeCustomerAsync(string email, string stripeCustomerId)
    {
        using var scope = _fixture.Factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.StripeCustomerId = stripeCustomerId;
        await db.SaveChangesAsync();
        return user;
    }

    private async Task SetSubscriptionEntitlementAsync(
        string email,
        string plan,
        DateTimeOffset? currentPeriodEndUtc,
        bool cancelAtPeriodEnd)
    {
        using var scope = _fixture.Factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.SubscriptionPlan = plan;
        user.SubscriptionCurrentPeriodEndUtc = currentPeriodEndUtc;
        user.SubscriptionCancelAtPeriodEnd = cancelAtPeriodEnd;
        await db.SaveChangesAsync();
    }

    private async Task<ApplicationUser> SetSubscriptionStateAsync(
        string email,
        string plan,
        DateTimeOffset? currentPeriodEndUtc,
        bool cancelAtPeriodEnd,
        string stripeCustomerId,
        string stripeSubscriptionId,
        string stripeSubscriptionStatus)
    {
        using var scope = _fixture.Factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.SubscriptionPlan = plan;
        user.SubscriptionCurrentPeriodEndUtc = currentPeriodEndUtc;
        user.SubscriptionCancelAtPeriodEnd = cancelAtPeriodEnd;
        user.StripeCustomerId = stripeCustomerId;
        user.StripeSubscriptionId = stripeSubscriptionId;
        user.StripeSubscriptionStatus = stripeSubscriptionStatus;
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<ApplicationUser> GetUserByEmailAsync(string email)
    {
        using var scope = _fixture.Factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        return await db.Users.SingleAsync(x => x.Email == email);
    }

    private async Task<StripeWebhookEvent> GetWebhookEventAsync(string stripeEventId)
    {
        using var scope = _fixture.Factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        return await db.StripeWebhookEvents.SingleAsync(x => x.StripeEventId == stripeEventId);
    }

    private static async Task<HttpResponseMessage> PostStripeWebhookAsync(HttpClient client, string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = EventUtility.ComputeSignature(WebhookSecret, timestamp, payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/billing/stripe-webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", $"t={timestamp},v1={signature}");
        return await client.SendAsync(request);
    }

    private static string CreateStripeEventPayload(
        string eventId,
        string eventType,
        object stripeObject,
        DateTimeOffset? eventCreatedUtc = null)
    {
        return JsonSerializer.Serialize(new
        {
            id = eventId,
            type = eventType,
            created = (eventCreatedUtc ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
            data = new
            {
                @object = stripeObject
            }
        });
    }

    private static object CreateSubscriptionPayload(
        string subscriptionId,
        string customerId,
        string status,
        DateTimeOffset currentPeriodEndUtc,
        string writeFluencyUserId)
    {
        return new
        {
            id = subscriptionId,
            customer = customerId,
            status,
            cancel_at_period_end = false,
            items = new
            {
                data = new[]
                {
                    new
                    {
                        current_period_end = currentPeriodEndUtc.ToUnixTimeSeconds()
                    }
                }
            },
            metadata = new
            {
                writefluency_user_id = writeFluencyUserId
            }
        };
    }

    private static object CreateInvoicePayload(string subscriptionId)
    {
        return new
        {
            id = $"in_{Guid.NewGuid():N}",
            parent = new
            {
                subscription_details = new
                {
                    subscription = subscriptionId
                }
            }
        };
    }

    private static async Task<HttpResponseMessage> PostAsJsonWithAllowedOriginAsync(
        HttpClient client,
        string requestUri,
        object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");
        return await client.SendAsync(request);
    }

    private static bool IsUnauthenticatedStatus(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Found or HttpStatusCode.Redirect;
    }

    private static string BuildUsersConfirmEmailUrlFromWebappLink(string html)
    {
        var confirmationUri = new Uri(ExtractHref(html), UriKind.Absolute);
        var query = QueryHelpers.ParseQuery(confirmationUri.Query);
        query.TryGetValue("userId", out var userId).ShouldBeTrue();
        query.TryGetValue("code", out var code).ShouldBeTrue();

        return QueryHelpers.AddQueryString("/users/auth/confirmEmail", new Dictionary<string, string?>
        {
            ["userId"] = userId.ToString(),
            ["code"] = code.ToString()
        });
    }

    private static string ExtractHref(string html)
    {
        var escapedHref = Regex.Match(html, "href=\\\\\\\"([^\\\\\\\"]+)\\\\\\\"", RegexOptions.IgnoreCase);
        if (escapedHref.Success)
        {
            return WebUtility.HtmlDecode(escapedHref.Groups[1].Value);
        }

        var normalHref = Regex.Match(html, "href=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        normalHref.Success.ShouldBeTrue("Expected to find confirmation link in email body");
        return WebUtility.HtmlDecode(normalHref.Groups[1].Value);
    }
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Billing;

public static class BillingEndpointExtensions
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var billingGroup = app.MapGroup("/billing").WithTags("Billing");

        billingGroup.MapPost("/checkout-session", CreateCheckoutSessionAsync)
            .RequireAuthorization();

        billingGroup.MapPost("/checkout-session/confirm", ConfirmCheckoutSessionAsync)
            .RequireAuthorization();

        billingGroup.MapPost("/portal-session", CreatePortalSessionAsync)
            .RequireAuthorization();

        billingGroup.MapPost("/sync", SyncSubscriptionAsync)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> CreateCheckoutSessionAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IStripeBillingClient stripeBillingClient,
        IOptions<StripeOptions> stripeOptions,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var entitlement = SubscriptionEntitlements.Build(user, DateTimeOffset.UtcNow);
        if (entitlement.Status is SubscriptionEntitlements.ProActiveStatus or SubscriptionEntitlements.ProCancelingStatus)
        {
            return Results.Ok(BillingCheckoutSessionResponse.ManagementRequired(entitlement));
        }

        if (!IsConfigured(stripeOptions.Value))
        {
            return Results.Problem(
                detail: "Stripe checkout is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            var customer = await stripeBillingClient.CreateCustomerAsync(user.Id, user.Email, cancellationToken);
            user.StripeCustomerId = customer.Id;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return Results.Problem(
                    detail: "Unable to persist Stripe customer.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        var session = await stripeBillingClient.CreateCheckoutSessionAsync(
            new StripeCheckoutSessionCreateRequest(
                UserId: user.Id,
                CustomerId: user.StripeCustomerId,
                ProMonthlyPriceId: stripeOptions.Value.ProMonthlyPriceId,
                SuccessUrl: stripeOptions.Value.SuccessUrl,
                CancelUrl: stripeOptions.Value.CancelUrl),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            return Results.Problem(
                detail: "Stripe did not return a Checkout URL.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Ok(BillingCheckoutSessionResponse.Created(session.Url, entitlement));
    }

    private static async Task<IResult> ConfirmCheckoutSessionAsync(
        [FromBody] ConfirmCheckoutSessionRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IStripeBillingClient stripeBillingClient,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId))
        {
            return Results.BadRequest(new { Error = "invalid_session_id" });
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            return Results.BadRequest(new { Error = "missing_stripe_customer" });
        }

        var session = await stripeBillingClient.GetCheckoutSessionAsync(request.SessionId.Trim(), cancellationToken);
        if (session is null || !string.Equals(session.Mode, "subscription", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { Error = "invalid_checkout_session" });
        }

        if (!string.Equals(session.CustomerId, user.StripeCustomerId, StringComparison.Ordinal)
            || !string.Equals(session.ClientReferenceId, user.Id, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(session.SubscriptionId))
        {
            return Results.BadRequest(new { Error = "missing_subscription" });
        }

        var subscription = await stripeBillingClient.GetSubscriptionAsync(session.SubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Results.BadRequest(new { Error = "invalid_subscription" });
        }

        if (!string.Equals(subscription.CustomerId, user.StripeCustomerId, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        ApplySubscriptionState(user, subscription);

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Results.Problem(
                detail: "Unable to persist subscription state.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(BillingEntitlementResponse.From(SubscriptionEntitlements.Build(user, DateTimeOffset.UtcNow)));
    }

    private static async Task<IResult> CreatePortalSessionAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IStripeBillingClient stripeBillingClient,
        IOptions<StripeOptions> stripeOptions,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var entitlement = SubscriptionEntitlements.Build(user, DateTimeOffset.UtcNow);
        if (entitlement.Status is not (SubscriptionEntitlements.ProActiveStatus or SubscriptionEntitlements.ProCancelingStatus))
        {
            var reason = entitlement.Status == SubscriptionEntitlements.ProExpiredStatus ? "pro_expired" : "free_user";
            return Results.Json(
                new { Error = "subscription_management_unavailable", Reason = reason },
                WebJsonOptions,
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(user.StripeCustomerId) || string.IsNullOrWhiteSpace(user.StripeSubscriptionId))
        {
            return Results.Json(
                new { Error = "missing_stripe_billing_reference" },
                WebJsonOptions,
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!IsPortalConfigured(stripeOptions.Value))
        {
            return Results.Problem(
                detail: "Stripe customer portal is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var session = await stripeBillingClient.CreatePortalSessionAsync(
                new StripePortalSessionCreateRequest(
                    CustomerId: user.StripeCustomerId,
                    PortalConfigurationId: stripeOptions.Value.PortalConfigurationId,
                    ReturnUrl: stripeOptions.Value.PortalReturnUrl),
                cancellationToken);

            if (string.IsNullOrWhiteSpace(session.Url))
            {
                return Results.Problem(
                    detail: "Stripe did not return a Customer Portal URL.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(new BillingPortalSessionResponse(session.Url));
        }
        catch (StripeException)
        {
            return Results.Problem(
                detail: "Stripe customer portal session creation failed.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> SyncSubscriptionAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IStripeBillingClient stripeBillingClient,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(user.StripeCustomerId) || string.IsNullOrWhiteSpace(user.StripeSubscriptionId))
        {
            return Results.Json(
                new { Error = "missing_stripe_billing_reference" },
                WebJsonOptions,
                statusCode: StatusCodes.Status409Conflict);
        }

        StripeSubscriptionResult? subscription;
        try
        {
            subscription = await stripeBillingClient.GetSubscriptionAsync(user.StripeSubscriptionId, cancellationToken);
        }
        catch (StripeException)
        {
            return Results.Problem(
                detail: "Stripe subscription sync failed.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        if (subscription is null)
        {
            return Results.BadRequest(new { Error = "invalid_subscription" });
        }

        if (!string.Equals(subscription.CustomerId, user.StripeCustomerId, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        ApplySubscriptionState(user, subscription);

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Results.Problem(
                detail: "Unable to persist subscription state.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(BillingEntitlementResponse.From(SubscriptionEntitlements.Build(user, DateTimeOffset.UtcNow)));
    }

    private static void ApplySubscriptionState(ApplicationUser user, StripeSubscriptionResult subscription)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var accessEndUtc = subscription.CancelAtUtc ?? subscription.CurrentPeriodEndUtc;

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
        return string.Equals(subscription.Status, "active", StringComparison.OrdinalIgnoreCase)
            && accessEndUtc > nowUtc;
    }

    private static bool IsSubscriptionCanceling(StripeSubscriptionResult subscription, DateTimeOffset nowUtc)
    {
        return subscription.CancelAtPeriodEnd
            || subscription.CancelAtUtc > nowUtc;
    }

    private static bool IsConfigured(StripeOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.SecretKey)
            && !string.IsNullOrWhiteSpace(options.ProMonthlyPriceId)
            && !string.IsNullOrWhiteSpace(options.SuccessUrl)
            && !string.IsNullOrWhiteSpace(options.CancelUrl);
    }

    private static bool IsPortalConfigured(StripeOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.SecretKey)
            && !string.IsNullOrWhiteSpace(options.PortalConfigurationId)
            && !string.IsNullOrWhiteSpace(options.PortalReturnUrl);
    }

    public sealed record ConfirmCheckoutSessionRequest([Required] string SessionId);

    private sealed record BillingPortalSessionResponse(string PortalUrl);

    private sealed record BillingCheckoutSessionResponse(
        string Status,
        string? CheckoutUrl,
        string Plan,
        string EntitlementStatus,
        bool IsPro,
        DateTimeOffset? CurrentPeriodEndUtc,
        bool CancelAtPeriodEnd)
    {
        public static BillingCheckoutSessionResponse Created(string checkoutUrl, SubscriptionEntitlement entitlement)
            => From("checkout_created", checkoutUrl, entitlement);

        public static BillingCheckoutSessionResponse ManagementRequired(SubscriptionEntitlement entitlement)
            => From("subscription_management_required", null, entitlement);

        private static BillingCheckoutSessionResponse From(
            string status,
            string? checkoutUrl,
            SubscriptionEntitlement entitlement)
        {
            return new BillingCheckoutSessionResponse(
                Status: status,
                CheckoutUrl: checkoutUrl,
                Plan: entitlement.Plan,
                EntitlementStatus: entitlement.Status,
                IsPro: entitlement.IsPro,
                CurrentPeriodEndUtc: entitlement.CurrentPeriodEndUtc,
                CancelAtPeriodEnd: entitlement.CancelAtPeriodEnd);
        }
    }

    private sealed record BillingEntitlementResponse(
        string Plan,
        string EntitlementStatus,
        bool IsPro,
        DateTimeOffset? CurrentPeriodEndUtc,
        bool CancelAtPeriodEnd)
    {
        public static BillingEntitlementResponse From(SubscriptionEntitlement entitlement)
        {
            return new BillingEntitlementResponse(
                Plan: entitlement.Plan,
                EntitlementStatus: entitlement.Status,
                IsPro: entitlement.IsPro,
                CurrentPeriodEndUtc: entitlement.CurrentPeriodEndUtc,
                CancelAtPeriodEnd: entitlement.CancelAtPeriodEnd);
        }
    }
}

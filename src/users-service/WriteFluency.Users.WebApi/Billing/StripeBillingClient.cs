using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Billing;

public interface IStripeBillingClient
{
    Task<StripeCustomerResult> CreateCustomerAsync(
        string userId,
        string? email,
        CancellationToken cancellationToken);

    Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        StripeCheckoutSessionCreateRequest request,
        CancellationToken cancellationToken);

    Task<StripeCheckoutSessionResult?> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task<StripeSubscriptionResult?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<StripePortalSessionResult> CreatePortalSessionAsync(
        StripePortalSessionCreateRequest request,
        CancellationToken cancellationToken);
}

public sealed class StripeBillingClient(IOptions<StripeOptions> options) : IStripeBillingClient
{
    private readonly StripeOptions _options = options.Value;

    public async Task<StripeCustomerResult> CreateCustomerAsync(
        string userId,
        string? email,
        CancellationToken cancellationToken)
    {
        var service = new CustomerService();
        var customer = await service.CreateAsync(
            new CustomerCreateOptions
            {
                Email = email,
                Metadata = new Dictionary<string, string>
                {
                    ["writefluency_user_id"] = userId
                }
            },
            RequestOptions(),
            cancellationToken);

        return new StripeCustomerResult(customer.Id);
    }

    public async Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        StripeCheckoutSessionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var service = new SessionService();
        var session = await service.CreateAsync(
            new SessionCreateOptions
            {
                Mode = "subscription",
                Customer = request.CustomerId,
                ClientReferenceId = request.UserId,
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = request.ProMonthlyPriceId,
                        Quantity = 1
                    }
                ],
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["writefluency_user_id"] = request.UserId
                    }
                }
            },
            RequestOptions(),
            cancellationToken);

        return new StripeCheckoutSessionResult(
            Id: session.Id,
            CustomerId: session.CustomerId,
            SubscriptionId: session.SubscriptionId,
            Url: session.Url,
            Mode: session.Mode,
            ClientReferenceId: session.ClientReferenceId,
            Status: session.Status);
    }

    public async Task<StripeCheckoutSessionResult?> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var service = new SessionService();
        try
        {
            var session = await service.GetAsync(sessionId, null, RequestOptions(), cancellationToken);
            return new StripeCheckoutSessionResult(
                Id: session.Id,
                CustomerId: session.CustomerId,
                SubscriptionId: session.SubscriptionId,
                Url: session.Url,
                Mode: session.Mode,
                ClientReferenceId: session.ClientReferenceId,
                Status: session.Status);
        }
        catch (StripeException ex) when (ex.StripeError?.Type == "invalid_request_error")
        {
            return null;
        }
    }

    public async Task<StripeSubscriptionResult?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var service = new SubscriptionService();
        try
        {
            var subscription = await service.GetAsync(subscriptionId, null, RequestOptions(), cancellationToken);
            return new StripeSubscriptionResult(
                Id: subscription.Id,
                CustomerId: subscription.CustomerId,
                Status: subscription.Status,
                CurrentPeriodEndUtc: subscription.Items?.Data.FirstOrDefault()?.CurrentPeriodEnd,
                CancelAtUtc: subscription.CancelAt,
                CancelAtPeriodEnd: subscription.CancelAtPeriodEnd);
        }
        catch (StripeException ex) when (ex.StripeError?.Type == "invalid_request_error")
        {
            return null;
        }
    }

    public async Task<StripePortalSessionResult> CreatePortalSessionAsync(
        StripePortalSessionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = request.CustomerId,
                Configuration = request.PortalConfigurationId,
                ReturnUrl = request.ReturnUrl
            },
            RequestOptions(),
            cancellationToken);

        return new StripePortalSessionResult(
            Id: session.Id,
            CustomerId: session.Customer,
            Url: session.Url,
            ReturnUrl: session.ReturnUrl);
    }

    private RequestOptions RequestOptions() => new()
    {
        ApiKey = _options.SecretKey
    };
}

public sealed record StripeCustomerResult(string Id);

public sealed record StripeCheckoutSessionCreateRequest(
    string UserId,
    string CustomerId,
    string ProMonthlyPriceId,
    string SuccessUrl,
    string CancelUrl);

public sealed record StripeCheckoutSessionResult(
    string Id,
    string? CustomerId,
    string? SubscriptionId,
    string? Url,
    string? Mode,
    string? ClientReferenceId,
    string? Status);

public sealed record StripeSubscriptionResult(
    string Id,
    string? CustomerId,
    string? Status,
    DateTimeOffset? CurrentPeriodEndUtc,
    DateTimeOffset? CancelAtUtc,
    bool CancelAtPeriodEnd);

public sealed record StripePortalSessionCreateRequest(
    string CustomerId,
    string PortalConfigurationId,
    string ReturnUrl);

public sealed record StripePortalSessionResult(
    string Id,
    string? CustomerId,
    string? Url,
    string? ReturnUrl);

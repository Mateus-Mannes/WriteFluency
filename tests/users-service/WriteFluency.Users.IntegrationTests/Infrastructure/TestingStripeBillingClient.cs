using WriteFluency.Users.WebApi.Billing;

namespace WriteFluency.Users.IntegrationTests.Infrastructure;

public sealed class TestingStripeBillingClient : IStripeBillingClient
{
    private int _customerSequence;
    private int _checkoutSessionSequence;

    public List<CreateCustomerRequest> CreatedCustomers { get; } = [];
    public List<CreateCheckoutSessionRequest> CreatedCheckoutSessions { get; } = [];
    public Dictionary<string, StripeCheckoutSessionResult> CheckoutSessions { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, StripeSubscriptionResult> Subscriptions { get; } = new(StringComparer.Ordinal);

    public void Reset()
    {
        _customerSequence = 0;
        _checkoutSessionSequence = 0;
        CreatedCustomers.Clear();
        CreatedCheckoutSessions.Clear();
        CheckoutSessions.Clear();
        Subscriptions.Clear();
    }

    public Task<StripeCustomerResult> CreateCustomerAsync(
        string userId,
        string? email,
        CancellationToken cancellationToken)
    {
        var customerId = $"cus_test_{++_customerSequence}";
        CreatedCustomers.Add(new CreateCustomerRequest(userId, email, customerId));
        return Task.FromResult(new StripeCustomerResult(customerId));
    }

    public Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        StripeCheckoutSessionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var sequence = ++_checkoutSessionSequence;
        var session = new StripeCheckoutSessionResult(
            Id: $"cs_test_{sequence}",
            CustomerId: request.CustomerId,
            SubscriptionId: $"sub_test_{sequence}",
            Url: $"https://checkout.stripe.test/session/{sequence}",
            Mode: "subscription",
            ClientReferenceId: request.UserId,
            Status: "open");

        CreatedCheckoutSessions.Add(new CreateCheckoutSessionRequest(
            request.UserId,
            request.CustomerId,
            request.ProMonthlyPriceId,
            request.SuccessUrl,
            request.CancelUrl,
            session.Id));
        CheckoutSessions[session.Id] = session;
        return Task.FromResult(session);
    }

    public Task<StripeCheckoutSessionResult?> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        CheckoutSessions.TryGetValue(sessionId, out var session);
        return Task.FromResult<StripeCheckoutSessionResult?>(session);
    }

    public Task<StripeSubscriptionResult?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        Subscriptions.TryGetValue(subscriptionId, out var subscription);
        return Task.FromResult<StripeSubscriptionResult?>(subscription);
    }

    public sealed record CreateCustomerRequest(string UserId, string? Email, string CustomerId);

    public sealed record CreateCheckoutSessionRequest(
        string UserId,
        string CustomerId,
        string ProMonthlyPriceId,
        string SuccessUrl,
        string CancelUrl,
        string SessionId);
}

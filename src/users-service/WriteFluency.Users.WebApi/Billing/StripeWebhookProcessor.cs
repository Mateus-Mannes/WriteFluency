using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stripe;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Billing;

public sealed class StripeWebhookProcessor(
    UsersDbContext dbContext,
    IStripeBillingClient stripeBillingClient,
    BillingSubscriptionSynchronizer subscriptionSynchronizer,
    ILogger<StripeWebhookProcessor> logger)
{
    private const int SlowProcessingThresholdMilliseconds = 5000;
    private const int MaxStoredErrorLength = 2000;

    public async Task ProcessAsync(
        Event stripeEvent,
        JsonElement stripeObject,
        DateTimeOffset? eventCreatedUtc,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var webhookEvent = await StartAttemptAsync(stripeEvent, cancellationToken);
        if (webhookEvent is null)
        {
            return;
        }

        try
        {
            var outcome = await ApplyEventAsync(stripeEvent.Type, stripeObject, eventCreatedUtc, cancellationToken);
            webhookEvent.ProcessingStatus = outcome;
            webhookEvent.ProcessedAtUtc = DateTimeOffset.UtcNow;
            webhookEvent.LastError = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Stripe webhook {StripeEventId} type {StripeEventType} completed with {StripeWebhookStatus} on attempt {StripeWebhookAttempt}",
                stripeEvent.Id,
                stripeEvent.Type,
                outcome,
                webhookEvent.AttemptCount);
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(stripeEvent.Id, ex, cancellationToken);
            logger.LogError(
                ex,
                "Stripe webhook {StripeEventId} type {StripeEventType} failed on attempt {StripeWebhookAttempt}",
                stripeEvent.Id,
                stripeEvent.Type,
                webhookEvent.AttemptCount);
            throw;
        }
        finally
        {
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (elapsedMilliseconds >= SlowProcessingThresholdMilliseconds)
            {
                logger.LogWarning(
                    "Stripe webhook {StripeEventId} type {StripeEventType} took {StripeWebhookDurationMilliseconds} ms",
                    stripeEvent.Id,
                    stripeEvent.Type,
                    elapsedMilliseconds);
            }
        }
    }

    private async Task<StripeWebhookEvent?> StartAttemptAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var webhookEvent = await dbContext.StripeWebhookEvents.FindAsync([stripeEvent.Id], cancellationToken);
        if (webhookEvent?.ProcessingStatus is StripeWebhookEventStatuses.Processed or StripeWebhookEventStatuses.Ignored)
        {
            logger.LogInformation(
                "Stripe webhook {StripeEventId} type {StripeEventType} was already handled as {StripeWebhookStatus}",
                stripeEvent.Id,
                stripeEvent.Type,
                webhookEvent.ProcessingStatus);
            return null;
        }

        if (webhookEvent is null)
        {
            webhookEvent = new StripeWebhookEvent
            {
                StripeEventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                ReceivedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.StripeWebhookEvents.Add(webhookEvent);
        }

        webhookEvent.EventType = stripeEvent.Type;
        webhookEvent.ProcessingStatus = StripeWebhookEventStatuses.Processing;
        webhookEvent.ProcessedAtUtc = null;
        webhookEvent.AttemptCount++;
        webhookEvent.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return webhookEvent;
    }

    private async Task<string> ApplyEventAsync(
        string eventType,
        JsonElement stripeObject,
        DateTimeOffset? eventCreatedUtc,
        CancellationToken cancellationToken)
    {
        return eventType switch
        {
            EventTypes.CheckoutSessionCompleted => await ApplyCheckoutCompletedAsync(
                stripeObject,
                eventCreatedUtc,
                cancellationToken),
            EventTypes.CustomerSubscriptionUpdated => await ApplyLatestSubscriptionAsync(
                RequiredString(stripeObject, "id"),
                eventCreatedUtc,
                cancellationToken),
            EventTypes.CustomerSubscriptionDeleted => await ApplyDeletedSubscriptionAsync(
                stripeObject,
                eventCreatedUtc,
                cancellationToken),
            EventTypes.InvoicePaid or EventTypes.InvoicePaymentFailed => await ApplyInvoiceSubscriptionAsync(
                stripeObject,
                eventCreatedUtc,
                cancellationToken),
            _ => StripeWebhookEventStatuses.Ignored
        };
    }

    private async Task<string> ApplyCheckoutCompletedAsync(
        JsonElement stripeObject,
        DateTimeOffset? eventCreatedUtc,
        CancellationToken cancellationToken)
    {
        var sessionId = RequiredString(stripeObject, "id");
        var session = await stripeBillingClient.GetCheckoutSessionAsync(sessionId, cancellationToken)
            ?? throw new StripeWebhookReconciliationException("Checkout Session could not be retrieved.");

        if (!string.Equals(session.Mode, "subscription", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(session.CustomerId)
            || string.IsNullOrWhiteSpace(session.SubscriptionId))
        {
            return StripeWebhookEventStatuses.Ignored;
        }

        var subscription = await stripeBillingClient.GetSubscriptionAsync(session.SubscriptionId, cancellationToken)
            ?? throw new StripeWebhookReconciliationException("Checkout subscription could not be retrieved.");
        var user = await FindUserAsync(
            subscription,
            session.ClientReferenceId,
            allowSubscriptionReplacement: true,
            cancellationToken);
        if (user is null)
        {
            return StripeWebhookEventStatuses.Ignored;
        }

        ValidateCustomer(subscription.CustomerId, session.CustomerId);
        return ApplySubscription(user, subscription, eventCreatedUtc, allowSubscriptionReplacement: true);
    }

    private async Task<string> ApplyLatestSubscriptionAsync(
        string subscriptionId,
        DateTimeOffset? eventCreatedUtc,
        CancellationToken cancellationToken)
    {
        var subscription = await stripeBillingClient.GetSubscriptionAsync(subscriptionId, cancellationToken)
            ?? throw new StripeWebhookReconciliationException("Subscription could not be retrieved.");
        var user = await FindUserAsync(subscription, null, allowSubscriptionReplacement: false, cancellationToken);
        if (user is null)
        {
            return StripeWebhookEventStatuses.Ignored;
        }

        return ApplySubscription(user, subscription, eventCreatedUtc, allowSubscriptionReplacement: false);
    }

    private async Task<string> ApplyDeletedSubscriptionAsync(
        JsonElement stripeObject,
        DateTimeOffset? eventCreatedUtc,
        CancellationToken cancellationToken)
    {
        var subscription = ParseSubscription(stripeObject);
        var user = await FindUserAsync(subscription, null, allowSubscriptionReplacement: false, cancellationToken);
        if (user is null)
        {
            return StripeWebhookEventStatuses.Ignored;
        }

        return ApplySubscription(
            user,
            subscription with { Status = "canceled", CancelAtPeriodEnd = false },
            eventCreatedUtc,
            allowSubscriptionReplacement: false);
    }

    private async Task<string> ApplyInvoiceSubscriptionAsync(
        JsonElement stripeObject,
        DateTimeOffset? eventCreatedUtc,
        CancellationToken cancellationToken)
    {
        var subscriptionId = ReadInvoiceSubscriptionId(stripeObject);
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return StripeWebhookEventStatuses.Ignored;
        }

        return await ApplyLatestSubscriptionAsync(subscriptionId, eventCreatedUtc, cancellationToken);
    }

    private async Task<ApplicationUser?> FindUserAsync(
        StripeSubscriptionResult subscription,
        string? fallbackUserId,
        bool allowSubscriptionReplacement,
        CancellationToken cancellationToken)
    {
        var metadataUserId = subscription.WriteFluencyUserId;
        var expectedUserId = !string.IsNullOrWhiteSpace(metadataUserId) ? metadataUserId : fallbackUserId;
        ApplicationUser? userByExpectedId = null;
        if (!string.IsNullOrWhiteSpace(expectedUserId))
        {
            userByExpectedId = await dbContext.Users.FindAsync([expectedUserId], cancellationToken)
                ?? throw new StripeWebhookReconciliationException("Referenced WriteFluency user does not exist.");
        }

        var userBySubscription = await dbContext.Users
            .SingleOrDefaultAsync(user => user.StripeSubscriptionId == subscription.Id, cancellationToken);
        ApplicationUser? userByCustomer = null;
        if (!string.IsNullOrWhiteSpace(subscription.CustomerId))
        {
            userByCustomer = await dbContext.Users
                .SingleOrDefaultAsync(user => user.StripeCustomerId == subscription.CustomerId, cancellationToken);
        }

        var candidates = new[] { userByExpectedId, userBySubscription, userByCustomer }
            .Where(user => user is not null)
            .Cast<ApplicationUser>()
            .DistinctBy(user => user.Id)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        if (candidates.Length > 1)
        {
            throw new StripeWebhookReconciliationException("Stripe references resolve to different WriteFluency users.");
        }

        var user = candidates[0];
        ValidateCustomer(user.StripeCustomerId, subscription.CustomerId);
        if (!CanApplySubscription(user, subscription.Id, allowSubscriptionReplacement))
        {
            return user;
        }

        if (!string.IsNullOrWhiteSpace(metadataUserId)
            && !string.Equals(metadataUserId, user.Id, StringComparison.Ordinal))
        {
            throw new StripeWebhookReconciliationException("Stripe subscription metadata does not match the resolved user.");
        }

        return user;
    }

    private static bool CanApplySubscription(
        ApplicationUser user,
        string subscriptionId,
        bool allowSubscriptionReplacement)
    {
        return allowSubscriptionReplacement
            || string.IsNullOrWhiteSpace(user.StripeSubscriptionId)
            || string.Equals(user.StripeSubscriptionId, subscriptionId, StringComparison.Ordinal);
    }

    private string ApplySubscription(
        ApplicationUser user,
        StripeSubscriptionResult subscription,
        DateTimeOffset? eventCreatedUtc,
        bool allowSubscriptionReplacement)
    {
        if (!CanApplySubscription(user, subscription.Id, allowSubscriptionReplacement)
            || IsOlderThanLastAppliedEvent(user, eventCreatedUtc))
        {
            return StripeWebhookEventStatuses.Ignored;
        }

        subscriptionSynchronizer.Apply(user, subscription, DateTimeOffset.UtcNow);
        if (eventCreatedUtc is not null)
        {
            user.StripeSubscriptionLastEventCreatedUtc = eventCreatedUtc;
        }

        return StripeWebhookEventStatuses.Processed;
    }

    private static bool IsOlderThanLastAppliedEvent(ApplicationUser user, DateTimeOffset? eventCreatedUtc)
    {
        return eventCreatedUtc is not null
            && user.StripeSubscriptionLastEventCreatedUtc > eventCreatedUtc;
    }

    private static void ValidateCustomer(string? expectedCustomerId, string? actualCustomerId)
    {
        if (string.IsNullOrWhiteSpace(expectedCustomerId)
            || string.IsNullOrWhiteSpace(actualCustomerId)
            || !string.Equals(expectedCustomerId, actualCustomerId, StringComparison.Ordinal))
        {
            throw new StripeWebhookReconciliationException("Stripe customer references do not match.");
        }
    }

    private async Task RecordFailureAsync(string stripeEventId, Exception exception, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var webhookEvent = await dbContext.StripeWebhookEvents.FindAsync([stripeEventId], cancellationToken);
        if (webhookEvent is null)
        {
            return;
        }

        webhookEvent.ProcessingStatus = StripeWebhookEventStatuses.Failed;
        webhookEvent.LastError = Truncate(exception.Message, MaxStoredErrorLength);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static StripeSubscriptionResult ParseSubscription(JsonElement stripeObject)
    {
        return new StripeSubscriptionResult(
            Id: RequiredString(stripeObject, "id"),
            CustomerId: OptionalString(stripeObject, "customer"),
            Status: OptionalString(stripeObject, "status"),
            CurrentPeriodEndUtc: ReadSubscriptionCurrentPeriodEnd(stripeObject),
            CancelAtUtc: OptionalUnixTimestamp(stripeObject, "cancel_at"),
            CancelAtPeriodEnd: OptionalBoolean(stripeObject, "cancel_at_period_end"),
            WriteFluencyUserId: OptionalNestedString(stripeObject, "metadata", "writefluency_user_id"));
    }

    private static DateTimeOffset? ReadSubscriptionCurrentPeriodEnd(JsonElement stripeObject)
    {
        if (!stripeObject.TryGetProperty("items", out var items)
            || !items.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array
            || data.GetArrayLength() == 0)
        {
            return null;
        }

        return OptionalUnixTimestamp(data[0], "current_period_end");
    }

    private static string? ReadInvoiceSubscriptionId(JsonElement stripeObject)
    {
        return OptionalNestedString(stripeObject, "parent", "subscription_details", "subscription")
            ?? OptionalNestedString(stripeObject, "parent", "subscription_details", "subscription_id")
            ?? OptionalString(stripeObject, "subscription");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        return OptionalString(element, propertyName)
            ?? throw new StripeWebhookReconciliationException($"Stripe payload is missing '{propertyName}'.");
    }

    private static string? OptionalNestedString(JsonElement element, params string[] propertyPath)
    {
        foreach (var propertyName in propertyPath)
        {
            if (!element.TryGetProperty(propertyName, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static bool OptionalBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static DateTimeOffset? OptionalUnixTimestamp(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt64(out var unixTimestamp)
                ? DateTimeOffset.FromUnixTimeSeconds(unixTimestamp)
                : null;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

public sealed class StripeWebhookReconciliationException(string message) : Exception(message);

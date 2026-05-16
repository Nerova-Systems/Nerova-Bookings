namespace Account.Features.Subscriptions.Shared;

/// <summary>
///     In-memory carrier for the webhook payload that
///     the endpoint just acknowledged. Phase 2 of two-phase webhook processing receives this record
///     directly from the endpoint so it never has to re-read the just-persisted webhook archive column.
///     The record lives at the <c>Subscriptions</c> feature layer, decoupled from the Paystack
///     integration boundary.
/// </summary>
public sealed record PendingWebhookEvent(
    string EventId,
    string EventType,
    DateTimeOffset PaystackCreatedAt,
    string Payload,
    string ApiVersion
);

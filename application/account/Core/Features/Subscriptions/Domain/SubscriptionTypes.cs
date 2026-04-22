using JetBrains.Annotations;

namespace Account.Features.Subscriptions.Domain;

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionPlan
{
    Trial = 0,
    Starter = 1,
    Standard = 2,
    Premium = 3
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Cancelled = 3,
    Expired = 4
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CancellationReason
{
    FoundAlternative,
    TooExpensive,
    NoLongerNeeded,
    Other,
    CancelledByAdmin
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentTransactionStatus
{
    Succeeded,
    Failed,
    Pending,
    Refunded
}

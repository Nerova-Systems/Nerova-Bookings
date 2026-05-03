using JetBrains.Annotations;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Subscriptions.Domain;

[PublicAPI]
[IdPrefix("CUS")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, PaystackCustomerId>))]
public sealed record PaystackCustomerId(string Value) : StronglyTypedString<PaystackCustomerId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

[PublicAPI]
[IdPrefix("SUB")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, PaystackSubscriptionId>))]
public sealed record PaystackSubscriptionId(string Value) : StronglyTypedString<PaystackSubscriptionId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionPlan
{
    Basis = 0,
    Standard = 1,
    Premium = 2
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

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaystackEventStatus
{
    Pending,
    Processed,
    Ignored,
    Failed
}

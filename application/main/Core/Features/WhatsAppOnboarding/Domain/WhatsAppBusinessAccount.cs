using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.WhatsAppOnboarding.Domain;

[PublicAPI]
[IdPrefix("waba")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WhatsAppBusinessAccountId>))]
public sealed record WhatsAppBusinessAccountId(string Value) : StronglyTypedUlid<WhatsAppBusinessAccountId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class WhatsAppBusinessAccount : AggregateRoot<WhatsAppBusinessAccountId>, ITenantScopedEntity
{
    private WhatsAppBusinessAccount(TenantId tenantId)
        : base(WhatsAppBusinessAccountId.NewId())
    {
        TenantId = tenantId;
        Status = WhatsAppBusinessAccountStatus.Connected;
    }

    public string MetaWabaId { get; private set; } = null!;

    public string BusinessName { get; private set; } = null!;

    /// <summary>
    ///     The Meta business access token, encrypted at rest with <c>IDataProtector</c>. Never persisted in
    ///     plaintext—callers must protect the token before constructing the aggregate and unprotect it on read.
    /// </summary>
    public string AccessToken { get; private set; } = null!;

    public WhatsAppBusinessAccountStatus Status { get; private set; }

    public WhatsAppPhoneNumber PhoneNumber { get; private set; } = null!;

    public TenantId TenantId { get; }

    public static WhatsAppBusinessAccount Create(TenantId tenantId, string metaWabaId, string businessName, string encryptedAccessToken, WhatsAppPhoneNumber phoneNumber)
    {
        return new WhatsAppBusinessAccount(tenantId)
        {
            MetaWabaId = metaWabaId,
            BusinessName = businessName,
            AccessToken = encryptedAccessToken,
            PhoneNumber = phoneNumber
        };
    }
}

[PublicAPI]
public sealed record WhatsAppPhoneNumber(string MetaPhoneNumberId, string DisplayPhoneNumber, string VerifiedName, PhoneNumberRegistrationStatus RegistrationStatus)
{
    public static WhatsAppPhoneNumber CreateRegistered(string metaPhoneNumberId, string displayPhoneNumber, string verifiedName)
    {
        return new WhatsAppPhoneNumber(metaPhoneNumberId, displayPhoneNumber, verifiedName, PhoneNumberRegistrationStatus.Registered);
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WhatsAppBusinessAccountStatus
{
    NotConnected,
    Connected
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PhoneNumberRegistrationStatus
{
    Pending,
    Registered
}

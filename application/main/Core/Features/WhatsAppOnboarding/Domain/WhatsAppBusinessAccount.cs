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

    /// <summary>Flow ID of the published WhatsApp Booking Flow under this tenant's WABA. Null until created at onboarding.</summary>
    public string? BookingFlowId { get; private set; }

    /// <summary>Flow ID of the published WhatsApp Login/Registration Flow under this tenant's WABA. Null until created at onboarding.</summary>
    public string? LoginFlowId { get; private set; }

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

    /// <summary>Stores the published WhatsApp Flow IDs provisioned for this tenant during or after onboarding.</summary>
    public void SetFlowIds(string? bookingFlowId, string? loginFlowId)
    {
        if (!string.IsNullOrWhiteSpace(bookingFlowId)) BookingFlowId = bookingFlowId;
        if (!string.IsNullOrWhiteSpace(loginFlowId)) LoginFlowId = loginFlowId;
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

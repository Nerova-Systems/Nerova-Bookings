using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.WhatsApp.Domain;

[PublicAPI]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<long, WabaConfigurationId>))]
public sealed record WabaConfigurationId(long Value) : StronglyTypedLongId<WabaConfigurationId>(Value)
{
    public override string ToString()
    {
        return Value.ToString();
    }
}

/// <summary>
///     Tracks the onboarding gate for a tenant's WhatsApp Business Account setup.
///     All four gates (WabaLinked, PhoneRegistered, KeyPairGenerated, PaystackConnected)
///     must be completed before the status advances to <see cref="Complete" />.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WabaOnboardingStatus
{
    Incomplete,
    WabaLinked,
    PhoneRegistered,
    KeyPairGenerated,
    PaystackConnected,
    Complete
}

/// <summary>
///     The status of the Meta Flow published to WhatsApp Flows.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WabaFlowStatus
{
    None,
    Draft,
    Published,
    Deprecated,
    NeedsUpdate
}

/// <summary>
///     Stores a tenant's WhatsApp Business Account (WABA) configuration, RSA key pair material,
///     Paystack subaccount code, and onboarding gate progress. One record per tenant.
/// </summary>
public sealed class WabaConfiguration : AggregateRoot<WabaConfigurationId>
{
    private WabaConfiguration(TenantId tenantId, string wabaId) : base(WabaConfigurationId.NewId())
    {
        TenantId = tenantId;
        WabaId = wabaId;
        FlowStatus = WabaFlowStatus.None;
        OnboardingGateStatus = WabaOnboardingStatus.Incomplete;
    }

    // Required by EF Core
    private WabaConfiguration() : base(WabaConfigurationId.NewId())
    {
    }

    public TenantId TenantId { get; private set; } = null!;

    /// <summary>Meta WABA ID returned from the Embedded Signup flow.</summary>
    public string WabaId { get; private set; } = string.Empty;

    /// <summary>Meta phone number ID — set after phone registration.</summary>
    public string? PhoneNumberId { get; private set; }

    /// <summary>Human-readable phone number, e.g. +27 81 123 4567.</summary>
    public string? DisplayPhoneNumber { get; private set; }

    /// <summary>SHA-256 fingerprint of our RSA public key, for verification with Meta.</summary>
    public string? PublicKeyFingerprint { get; private set; }

    /// <summary>AES-256-GCM encrypted RSA private key, base64-encoded.</summary>
    public string? EncryptedPrivateKey { get; private set; }

    /// <summary>Base64-encoded IV for the AES-256-GCM encryption of the private key.</summary>
    public string? PrivateKeyIv { get; private set; }

    /// <summary>Meta Flow ID — set after flow creation.</summary>
    public string? FlowId { get; private set; }

    public WabaFlowStatus FlowStatus { get; private set; }

    /// <summary>Paystack subaccount code — set during onboarding.</summary>
    public string? SubaccountCode { get; private set; }

    public WabaOnboardingStatus OnboardingGateStatus { get; private set; }

    /// <summary>
    ///     Long-lived WABA system-user access token used to call the Meta Graph API on behalf of
    ///     this tenant (flow create/upload/publish). Stored encrypted at rest in a future
    ///     iteration — currently held in plaintext on the configuration record.
    /// </summary>
    public string? WabaAccessToken { get; private set; }

    /// <summary>
    ///     Most recent Flow JSON published to Meta. Cached so the questionnaire screen can render
    ///     a deterministic preview without re-running the template engine.
    /// </summary>
    public string? GeneratedFlowJson { get; private set; }

    public static WabaConfiguration Create(TenantId tenantId, string wabaId, string? phoneNumberId, string? displayPhoneNumber)
    {
        var config = new WabaConfiguration(tenantId, wabaId)
        {
            PhoneNumberId = phoneNumberId,
            DisplayPhoneNumber = displayPhoneNumber,
            OnboardingGateStatus = WabaOnboardingStatus.WabaLinked
        };
        return config;
    }

    /// <summary>
    ///     Updates the Meta WABA link fields and advances onboarding to <see cref="WabaOnboardingStatus.WabaLinked" />
    ///     if the current status is <see cref="WabaOnboardingStatus.Incomplete" />.
    /// </summary>
    public void LinkWaba(string wabaId, string? phoneNumberId, string? displayPhoneNumber)
    {
        WabaId = wabaId;
        PhoneNumberId = phoneNumberId;
        DisplayPhoneNumber = displayPhoneNumber;

        if (OnboardingGateStatus == WabaOnboardingStatus.Incomplete)
        {
            OnboardingGateStatus = WabaOnboardingStatus.WabaLinked;
        }
    }

    /// <summary>
    ///     Stores the encrypted RSA key pair and advances onboarding to
    ///     <see cref="WabaOnboardingStatus.KeyPairGenerated" /> when the prior gates have been completed.
    /// </summary>
    public void SetKeyPair(string encryptedPrivateKey, string privateKeyIv, string publicKeyFingerprint)
    {
        EncryptedPrivateKey = encryptedPrivateKey;
        PrivateKeyIv = privateKeyIv;
        PublicKeyFingerprint = publicKeyFingerprint;

        if (OnboardingGateStatus is WabaOnboardingStatus.WabaLinked or WabaOnboardingStatus.PhoneRegistered)
        {
            OnboardingGateStatus = WabaOnboardingStatus.KeyPairGenerated;
        }
    }

    /// <summary>
    ///     Stores the Paystack subaccount code and advances onboarding to
    ///     <see cref="WabaOnboardingStatus.PaystackConnected" />, or to
    ///     <see cref="WabaOnboardingStatus.Complete" /> when all other gates are already satisfied.
    /// </summary>
    public void SetSubaccountCode(string subaccountCode)
    {
        SubaccountCode = subaccountCode;

        var allOtherGatesDone = WabaId != string.Empty
                                && PhoneNumberId is not null
                                && EncryptedPrivateKey is not null
                                && PublicKeyFingerprint is not null;

        OnboardingGateStatus = allOtherGatesDone
            ? WabaOnboardingStatus.Complete
            : WabaOnboardingStatus.PaystackConnected;
    }

    public void SetWabaAccessToken(string accessToken)
    {
        WabaAccessToken = accessToken;
    }

    public void SetFlowId(string flowId)
    {
        FlowId = flowId;
    }

    public void SetFlowStatus(WabaFlowStatus status)
    {
        FlowStatus = status;
    }

    public void SetGeneratedFlowJson(string flowJson)
    {
        GeneratedFlowJson = flowJson;
    }
}

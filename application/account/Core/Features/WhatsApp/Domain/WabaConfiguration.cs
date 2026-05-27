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
///     Local domain projection of the Meta WABA display-name review status. Maps the wire-level
///     <see cref="MetaNameStatus" /> values to a smaller set of states we care about: the
///     terminal-but-distinct results (<see cref="Approved" />, <see cref="Declined" />,
///     <see cref="Expired" />), the only state that blocks new requests
///     (<see cref="PendingReview" />), and a default <see cref="None" /> for tenants that have
///     never requested a change.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WabaDisplayNameStatus
{
    None,
    PendingReview,
    Approved,
    Declined,
    Expired
}

/// <summary>
///     Wire-level <c>name_status</c> returned by Meta on
///     <c>GET /{phone-number-id}?fields=name_status,verified_name</c>. The names match Meta's
///     string codes so the API client can do an exact case-sensitive parse.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetaNameStatus
{
    APPROVED,
    AVAILABLE_WITHOUT_REVIEW,
    DECLINED,
    EXPIRED,
    PENDING_REVIEW,
    NONE
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

    // ─── Display-name review state (Phase 7c) ────────────────────────────
    // Meta reviews display-name changes for 1–3 business days. The fields below track the
    // in-flight request so callers can render the pending banner, and the poller can detect a
    // terminal transition (APPROVED / DECLINED / EXPIRED) without losing the requested name on
    // restart.

    /// <summary>The display name we asked Meta to assign on the most recent request.</summary>
    public string? RequestedDisplayName { get; private set; }

    /// <summary>
    ///     Local projection of Meta's <c>name_status</c>. Defaults to <see cref="WabaDisplayNameStatus.None" />
    ///     for tenants that have never requested a change.
    /// </summary>
    public WabaDisplayNameStatus DisplayNameStatus { get; private set; }

    /// <summary>When the tenant submitted the most recent display-name change request.</summary>
    public DateTimeOffset? DisplayNameReviewRequestedAt { get; private set; }

    /// <summary>When the poller last asked Meta for the current <c>name_status</c>.</summary>
    public DateTimeOffset? DisplayNameLastCheckedAt { get; private set; }

    /// <summary>
    ///     Meta's <c>verified_name</c> — the currently-displayed name on WhatsApp, which trails
    ///     <see cref="RequestedDisplayName" /> until Meta approves the change.
    /// </summary>
    public string? VerifiedName { get; private set; }

    /// <summary>
    ///     Records a new display-name change request and moves the aggregate to
    ///     <see cref="WabaDisplayNameStatus.PendingReview" />. Meta forbids submitting a new
    ///     request while a previous one is in review, so we enforce the same invariant locally to
    ///     avoid the round-trip and the 4xx that would follow.
    /// </summary>
    /// <param name="requested">The name to submit to Meta. Caller must validate format first.</param>
    /// <param name="now">Timestamp for the request — supplied for deterministic tests.</param>
    public void RequestDisplayNameChange(string requested, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requested);

        if (DisplayNameStatus == WabaDisplayNameStatus.PendingReview)
        {
            throw new InvalidOperationException(
                "Cannot request a new display name while the previous request is pending review."
            );
        }

        RequestedDisplayName = requested;
        DisplayNameStatus = WabaDisplayNameStatus.PendingReview;
        DisplayNameReviewRequestedAt = now;
    }

    /// <summary>
    ///     Applies a poller result. Maps the wire-level <see cref="MetaNameStatus" /> into the
    ///     local <see cref="WabaDisplayNameStatus" /> and refreshes
    ///     <see cref="VerifiedName" /> + <see cref="DisplayNameLastCheckedAt" />.
    ///     <para>
    ///         <c>APPROVED</c> and <c>AVAILABLE_WITHOUT_REVIEW</c> both collapse to
    ///         <see cref="WabaDisplayNameStatus.Approved" />: the latter is what Meta returns when
    ///         a brand exemption skipped the review queue, and downstream consumers treat the two
    ///         states identically.
    ///     </para>
    /// </summary>
    public void MarkDisplayNameReviewResult(MetaNameStatus metaStatus, string? verifiedName, DateTimeOffset now)
    {
        DisplayNameStatus = metaStatus switch
        {
            MetaNameStatus.APPROVED or MetaNameStatus.AVAILABLE_WITHOUT_REVIEW => WabaDisplayNameStatus.Approved,
            MetaNameStatus.DECLINED => WabaDisplayNameStatus.Declined,
            MetaNameStatus.EXPIRED => WabaDisplayNameStatus.Expired,
            MetaNameStatus.PENDING_REVIEW => WabaDisplayNameStatus.PendingReview,
            MetaNameStatus.NONE => WabaDisplayNameStatus.None,
            _ => DisplayNameStatus
        };

        VerifiedName = verifiedName;
        DisplayNameLastCheckedAt = now;
    }

    /// <summary>
    ///     Attempts to copy <see cref="VerifiedName" /> back to the tenant's
    ///     <see cref="Tenants.Domain.BrandProfile.BusinessDisplayName" />. The sync is guarded by two
    ///     conditions:
    ///     <list type="bullet">
    ///         <item>The display-name review must have reached <see cref="WabaDisplayNameStatus.Approved" />.</item>
    ///         <item>
    ///             The brand profile's <see cref="Tenants.Domain.BrandProfile.BusinessDisplayName" /> must still
    ///             equal <see cref="RequestedDisplayName" /> — if the tenant edited it locally in the meantime
    ///             their version is left intact.
    ///         </item>
    ///     </list>
    ///     Returns the updated <see cref="Tenants.Domain.BrandProfile" /> when a sync is warranted, or
    ///     <see langword="null" /> when the conditions are not met.
    /// </summary>
    public Tenants.Domain.BrandProfile? TrySyncVerifiedNameToBrandProfile(Tenants.Domain.BrandProfile? brandProfile)
    {
        if (DisplayNameStatus != WabaDisplayNameStatus.Approved) return null;
        if (VerifiedName is null) return null;
        if (brandProfile is null) return null;
        if (brandProfile.BusinessDisplayName != RequestedDisplayName) return null;

        return brandProfile.WithBusinessDisplayName(VerifiedName);
    }
}

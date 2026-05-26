using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Apps.Domain;

[IdPrefix("crd")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, CredentialId>))]
public sealed record CredentialId(string Value) : StronglyTypedUlid<CredentialId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Per-user OAuth credential for a specific <see cref="App" /> (identified by
///     <see cref="AppSlug" />). The <see cref="EncryptedKey" /> column holds the encrypted JSON
///     blob containing access token, refresh token, and expiry — encryption is performed by
///     <c>CredentialProtector</c> at write time and never persisted in cleartext.
///     <para>
///         Mirrors cal.com's <c>Credential</c> table. A user has at most one credential per
///         <c>(TenantId, UserId, AppSlug)</c> tuple.
///     </para>
/// </summary>
public sealed class Credential : AggregateRoot<CredentialId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Credential() : base(CredentialId.NewId())
    {
        UserId = new UserId(string.Empty);
        AppSlug = new AppSlug(string.Empty);
        EncryptedKey = string.Empty;
    }

    private Credential(TenantId tenantId, UserId userId, AppSlug appSlug, string encryptedKey)
        : base(CredentialId.NewId())
    {
        TenantId = tenantId;
        UserId = userId;
        AppSlug = appSlug;
        EncryptedKey = encryptedKey;
    }

    public UserId UserId { get; private set; }

    public AppSlug AppSlug { get; private set; }

    /// <summary>
    ///     Encrypted JSON blob carrying the access token, refresh token, expiry, and any other
    ///     fields the connector needs. The encryption layer (<c>CredentialProtector</c>) is the
    ///     only component permitted to read this column as plaintext.
    /// </summary>
    public string EncryptedKey { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static Credential Create(TenantId tenantId, UserId userId, AppSlug appSlug, string encryptedKey)
    {
        if (string.IsNullOrEmpty(encryptedKey))
        {
            throw new ArgumentException("Credential key cannot be empty.", nameof(encryptedKey));
        }

        return new Credential(tenantId, userId, appSlug, encryptedKey);
    }

    public void UpdateKey(string encryptedKey)
    {
        if (string.IsNullOrEmpty(encryptedKey))
        {
            throw new ArgumentException("Credential key cannot be empty.", nameof(encryptedKey));
        }

        EncryptedKey = encryptedKey;
    }
}

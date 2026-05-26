using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.ApiKeys.Domain;

/// <summary>
///     Strongly-typed identifier for an <see cref="ApiKey" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>key</c>.
/// </summary>
[PublicAPI]
[IdPrefix("key")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, ApiKeyId>))]
public sealed record ApiKeyId(string Value) : StronglyTypedUlid<ApiKeyId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A long-lived API key that authenticates a user or organisation without requiring interactive login.
///     <para>
///         The plaintext token is generated once in <see cref="CreateUserKey" /> / <see cref="CreateOrgKey" />
///         and is never persisted. Only the SHA-256 hex digest (<see cref="KeyHash" />) is stored.
///         The first 12 characters of the plaintext are stored as <see cref="KeyPrefix" /> for display purposes.
///     </para>
///     <para>
///         User-scope keys (<see cref="ApiKeyScope.User" />) are owned by a personal (solo) tenant.
///         Organisation-scope keys (<see cref="ApiKeyScope.Organization" />) are owned by an org tenant.
///     </para>
/// </summary>
public sealed class ApiKey : AggregateRoot<ApiKeyId>, ITenantScopedEntity
{
    private ApiKey(ApiKeyId id) : base(id)
    {
    }

    /// <summary>Human-readable label set by the key creator.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Whether this key is scoped to a user or an organisation.</summary>
    public ApiKeyScope Scope { get; private set; }

    /// <summary>SHA-256 hex digest of the plaintext token. Used for constant-time lookup.</summary>
    public string KeyHash { get; private set; } = null!;

    /// <summary>First 12 characters of the plaintext token. Safe to display for identification.</summary>
    public string KeyPrefix { get; private set; } = null!;

    /// <summary>Optional expiry. A <see langword="null" /> value means the key never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private init; }

    /// <summary>Set when the key is explicitly revoked. A revoked key cannot authenticate.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Updated on every successful authentication. May be <see langword="null" /> if never used.</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>The user who created this key. Used to populate authentication context.</summary>
    public UserId CreatedByUserId { get; private set; } = null!;

    /// <summary>The tenant that owns this key (solo tenant for user keys, org tenant for org keys).</summary>
    public TenantId TenantId { get; private init; } = null!;

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>Creates a new user-scope API key and returns it together with the one-time plaintext token.</summary>
    public static (ApiKey Key, string PlainText) CreateUserKey(
        TenantId tenantId,
        UserId userId,
        string name,
        DateTimeOffset? expiresAt)
    {
        var (plainText, hash, prefix) = GenerateToken("nerova_user_");
        return (new ApiKey(ApiKeyId.NewId())
        {
            TenantId = tenantId,
            Name = name,
            Scope = ApiKeyScope.User,
            KeyHash = hash,
            KeyPrefix = prefix,
            ExpiresAt = expiresAt,
            CreatedByUserId = userId
        }, plainText);
    }

    /// <summary>Creates a new organisation-scope API key and returns it together with the one-time plaintext token.</summary>
    public static (ApiKey Key, string PlainText) CreateOrgKey(
        TenantId orgTenantId,
        UserId createdByUserId,
        string name,
        DateTimeOffset? expiresAt)
    {
        var (plainText, hash, prefix) = GenerateToken("nerova_org_");
        return (new ApiKey(ApiKeyId.NewId())
        {
            TenantId = orgTenantId,
            Name = name,
            Scope = ApiKeyScope.Organization,
            KeyHash = hash,
            KeyPrefix = prefix,
            ExpiresAt = expiresAt,
            CreatedByUserId = createdByUserId
        }, plainText);
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    /// <summary>Records that the key was successfully used for authentication at <paramref name="now" />.</summary>
    public void MarkUsed(DateTimeOffset now)
    {
        LastUsedAt = now;
    }

    /// <summary>Permanently revokes the key. Revoked keys fail validation immediately.</summary>
    public void Revoke(DateTimeOffset now)
    {
        RevokedAt = now;
    }

    // ─── Predicates ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Returns <see langword="true" /> when the key has not been revoked and has not passed its expiry.
    /// </summary>
    public bool IsValid(DateTimeOffset now)
    {
        return RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static (string PlainText, string Hash, string Prefix) GenerateToken(string prefixString)
    {
        Span<byte> randomBytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(randomBytes);

        // URL-safe base64 without padding, with substitutions to keep the token alphanumeric.
        var base64 = Convert.ToBase64String(randomBytes)
            .Replace('+', 'A')
            .Replace('/', 'B')
            .TrimEnd('=');

        var plainText = prefixString + base64;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainText))).ToLowerInvariant();
        var prefix = plainText[..Math.Min(12, plainText.Length)];
        return (plainText, hash, prefix);
    }
}

public interface IApiKeyRepository : ICrudRepository<ApiKey, ApiKeyId>
{
    /// <summary>Looks up a key by its SHA-256 hash, ignoring all tenant filters.</summary>
    Task<ApiKey?> GetByHashAsync(string hash, CancellationToken cancellationToken);

    /// <summary>Returns all non-revoked keys belonging to a user's solo tenant (user-scope only).</summary>
    Task<IReadOnlyList<ApiKey>> GetByUserAsync(TenantId userTenantId, CancellationToken cancellationToken);

    /// <summary>Returns all non-revoked keys belonging to an organisation, bypassing tenant filters.</summary>
    Task<IReadOnlyList<ApiKey>> GetByOrgAsync(TenantId orgTenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Fetches a key by its ID without applying tenant filters.
    ///     Required for org-scope revocation where the key's tenant differs from the caller's solo tenant.
    /// </summary>
    Task<ApiKey?> GetByIdUnfilteredAsync(ApiKeyId id, CancellationToken cancellationToken);
}

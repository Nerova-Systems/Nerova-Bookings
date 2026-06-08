using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.WhatsAppBooking.Domain;

[PublicAPI]
[IdPrefix("walch")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WhatsAppLoginChallengeId>))]
public sealed record WhatsAppLoginChallengeId(string Value) : StronglyTypedUlid<WhatsAppLoginChallengeId>(Value)
{
    public override string ToString() => Value;
}

/// <summary>
///     A one-time OTP challenge issued to an unidentified WhatsApp customer during the login/registration
///     Flow. The 6-digit OTP is hashed at rest; only the hash and a random salt are persisted. The plain
///     OTP is returned from <see cref="Create" /> so the caller can email it immediately.
///     One challenge per tenant+phone — replaced (deleted and re-created) on each new request.
/// </summary>
public sealed class WhatsAppLoginChallenge : AggregateRoot<WhatsAppLoginChallengeId>, ITenantScopedEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    [UsedImplicitly]
    private WhatsAppLoginChallenge() : base(WhatsAppLoginChallengeId.NewId())
    {
        PhoneNumber = string.Empty;
        Email = string.Empty;
        OtpHash = string.Empty;
        OtpSalt = string.Empty;
    }

    private WhatsAppLoginChallenge(TenantId tenantId) : base(WhatsAppLoginChallengeId.NewId())
    {
        TenantId = tenantId;
    }

    public TenantId TenantId { get; } = new(0);

    /// <summary>Customer WhatsApp phone number in E.164 format.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    /// <summary>Lowercase hex SHA-256 of (otp + salt). Never the plain OTP.</summary>
    public string OtpHash { get; private set; } = string.Empty;

    public string OtpSalt { get; private set; } = string.Empty;

    public bool IsConsumed { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    ///     Creates a new challenge and returns both the aggregate and the plain 6-digit OTP.
    ///     The OTP must be emailed to the customer immediately; it is not persisted.
    /// </summary>
    public static (WhatsAppLoginChallenge Challenge, string PlainOtp) Create(
        TenantId tenantId, string phoneNumber, string email, DateTimeOffset now)
    {
        var otp = Random.Shared.Next(100_000, 999_999).ToString();
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var hash = Hash(otp, salt);

        var challenge = new WhatsAppLoginChallenge(tenantId)
        {
            PhoneNumber = phoneNumber,
            Email = email,
            OtpHash = hash,
            OtpSalt = salt,
            IsConsumed = false,
            ExpiresAt = now + Lifetime
        };

        return (challenge, otp);
    }

    /// <summary>Returns true when the submitted OTP matches and the challenge is still valid.</summary>
    public bool Validate(string otp, DateTimeOffset now)
    {
        return !IsConsumed && ExpiresAt > now
               && string.Equals(Hash(otp, OtpSalt), OtpHash, StringComparison.Ordinal);
    }

    /// <summary>Marks the challenge consumed so it cannot be replayed.</summary>
    public void Consume() => IsConsumed = true;

    private static string Hash(string otp, string salt)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp + salt))).ToLowerInvariant();
}

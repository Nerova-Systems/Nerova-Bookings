using System.Text.Json;

namespace Main.Features.Apps.Connectors.Office365Calendar;

/// <summary>
///     The decrypted shape of an Office 365 Calendar <c>Credential.EncryptedKey</c> blob.
///     Mirrors cal.com's <c>office365_calendarCredentialSchema</c> — access token, refresh
///     token, expiry, the authoritative space-separated <c>scope</c> string Microsoft
///     returns, and the user principal name (email) we use as the organizer when creating
///     events.
/// </summary>
public sealed record Office365CredentialBlob(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string Scope,
    string? UserPrincipalName = null
)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public static Office365CredentialBlob FromJson(string json)
    {
        return JsonSerializer.Deserialize<Office365CredentialBlob>(json, JsonOptions)
               ?? throw new InvalidOperationException("Office 365 credential blob deserialized to null.");
    }
}

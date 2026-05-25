using System.Text.Json;

namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>
///     The decrypted shape of a Google Calendar <c>Credential.EncryptedKey</c> blob. Mirrors the
///     cal.com <c>googleCredentialSchema</c> — access token, refresh token, expiry, and the
///     authoritative space-separated <c>scope</c> string Google returns at token-exchange time.
/// </summary>
public sealed record GoogleCredentialBlob(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string Scope
)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public static GoogleCredentialBlob FromJson(string json)
    {
        return JsonSerializer.Deserialize<GoogleCredentialBlob>(json, JsonOptions)
            ?? throw new InvalidOperationException("Google credential blob deserialized to null.");
    }
}

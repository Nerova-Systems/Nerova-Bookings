using System.Text.Json;

namespace Main.Features.Apps.Connectors.Zoom;

/// <summary>
///     The decrypted shape of a Zoom <c>Credential.EncryptedKey</c> blob. Mirrors the cal.com
///     <c>zoom_video</c> credential schema — access token, refresh token, expiry, and the
///     authoritative space-separated <c>scope</c> string Zoom returns at token-exchange time.
/// </summary>
public sealed record ZoomCredentialBlob(
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

    public static ZoomCredentialBlob FromJson(string json)
    {
        return JsonSerializer.Deserialize<ZoomCredentialBlob>(json, JsonOptions)
            ?? throw new InvalidOperationException("Zoom credential blob deserialized to null.");
    }
}

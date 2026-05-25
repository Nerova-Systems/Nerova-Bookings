using System.Security.Cryptography;
using System.Text;

namespace Main.Features.Webhooks.Infrastructure;

/// <summary>
///     HMAC-SHA256 signer used to authenticate outbound webhook deliveries to subscriber endpoints.
///     Output bytes match cal.com's <c>sendPayload</c> (lowercase hex of
///     <c>HMAC-SHA256(secret, body)</c>). The wire format adds a <c>sha256=</c> prefix per
///     T0-webhook-platform task spec — subscribers strip the prefix before comparing.
/// </summary>
public static class WebhookSigner
{
    public const string HeaderName = "X-Cal-Signature-256";

    public static string Sign(string secret, string body)
    {
        if (secret is null) throw new ArgumentNullException(nameof(secret));
        if (body is null) throw new ArgumentNullException(nameof(body));

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var hash = HMACSHA256.HashData(keyBytes, bodyBytes);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }
}

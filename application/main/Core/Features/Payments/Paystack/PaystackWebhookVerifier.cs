using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace Main.Features.Payments.Paystack;

/// <summary>Outcome of parsing + verifying a Paystack booking webhook payload.</summary>
[PublicAPI]
public sealed record PaystackBookingWebhookEvent(string EventId, string EventType, string Reference);

public interface IPaystackWebhookVerifier
{
    /// <summary>
    ///     Validates the <c>x-paystack-signature</c> header (HMAC-SHA512 of the raw payload using the
    ///     configured secret key) and parses the event id / type / booking reference. Returns
    ///     <c>null</c> if the signature is invalid, the payload is malformed, or the configured
    ///     secret key is missing — callers should respond 401/400 accordingly.
    /// </summary>
    PaystackBookingWebhookEvent? Verify(string payload, string signatureHeader);
}

public sealed class PaystackWebhookVerifier(
    IConfiguration configuration,
    ILogger<PaystackWebhookVerifier> logger
) : IPaystackWebhookVerifier
{
    public PaystackBookingWebhookEvent? Verify(string payload, string signatureHeader)
    {
        var secretKey = configuration["Paystack:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            logger.LogWarning("Paystack:SecretKey is not configured. Rejecting webhook.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var expectedSignature = Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(payload))
        ).ToLowerInvariant();

        var expected = Encoding.UTF8.GetBytes(expectedSignature);
        var actual = Encoding.UTF8.GetBytes(signatureHeader.Trim().ToLowerInvariant());

        if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventType = TryGetString(root, "event") ?? "unknown";
            var data = root.TryGetProperty("data", out var dataElement) ? dataElement : default;
            var reference = data.ValueKind == JsonValueKind.Object ? TryGetString(data, "reference") : null;
            var eventId = data.ValueKind == JsonValueKind.Object ? TryGetString(data, "id") : null;

            // Fall back to a payload hash so re-deliveries with no event id are still idempotent.
            eventId ??= reference ?? $"{eventType}_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()}";

            return new PaystackBookingWebhookEvent(eventId, eventType, reference ?? string.Empty);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid Paystack booking webhook payload");
            return null;
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}

using System.Text.Json;

namespace Main.Features.WhatsAppBooking.Shared;

/// <summary>
///     Parses the <c>response_json</c> submitted by the WhatsApp Login/Registration Flow (nfm_reply).
///     The Flow must include fields named <c>name</c> and <c>email</c>. <c>phone</c> is optional and
///     display-only — the engine always trusts the verified WhatsApp sender. The OTP is validated
///     server-side by the Flows data endpoint before the Flow reaches completion.
/// </summary>
public sealed class WhatsAppLoginFlowResponse
{
    public string? Name { get; private init; }

    public string? Email { get; private init; }

    public string? Phone { get; private init; }

    /// <summary>Returns null when the JSON is missing or required fields are absent/empty.</summary>
    public static WhatsAppLoginFlowResponse? TryParse(string? responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            var phone = root.TryGetProperty("phone", out var p) ? p.GetString() : null;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return new WhatsAppLoginFlowResponse
            {
                Name = name.Trim(),
                Email = email.Trim().ToLowerInvariant(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim()
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

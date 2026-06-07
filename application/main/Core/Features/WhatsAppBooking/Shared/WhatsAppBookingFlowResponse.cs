using System.Text.Json;

namespace Main.Features.WhatsAppBooking.Shared;

/// <summary>
///     The booking details a customer submits through the WhatsApp booking Flow, parsed from the Flow
///     completion (<c>nfm_reply</c>) <c>response_json</c>. Field names match the Flow's terminal-screen payload.
///     Nullable because the payload is attacker/Meta-controlled; the engine validates before creating a booking.
/// </summary>
public sealed record WhatsAppBookingFlowResponse(
    [property: JsonPropertyName("event_slug")] string? EventSlug,
    [property: JsonPropertyName("start_time")] DateTimeOffset? StartTime,
    [property: JsonPropertyName("duration")] int? DurationMinutes,
    [property: JsonPropertyName("timezone")] string? TimeZone,
    [property: JsonPropertyName("booker_name")] string? BookerName,
    [property: JsonPropertyName("booker_email")] string? BookerEmail
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    ///     Attempts to parse the Flow completion response JSON. Returns null when the payload is absent, malformed,
    ///     or missing the fields required to create a booking. Never throws.
    /// </summary>
    public static WhatsAppBookingFlowResponse? TryParse(string? responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<WhatsAppBookingFlowResponse>(responseJson, JsonOptions);
            return parsed is null || !parsed.IsBookable() ? null : parsed;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>True when every field required to create a booking is present and well-formed.</summary>
    public bool IsBookable()
    {
        return !string.IsNullOrWhiteSpace(EventSlug)
            && StartTime is not null
            && DurationMinutes is > 0
            && !string.IsNullOrWhiteSpace(TimeZone)
            && !string.IsNullOrWhiteSpace(BookerName)
            && !string.IsNullOrWhiteSpace(BookerEmail);
    }
}

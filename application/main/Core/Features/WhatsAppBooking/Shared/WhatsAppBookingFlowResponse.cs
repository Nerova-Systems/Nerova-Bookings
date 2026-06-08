using System.Text.Json;

namespace Main.Features.WhatsAppBooking.Shared;

/// <summary>
///     The booking details a customer submits through the WhatsApp booking Flow, parsed from the Flow
///     completion (<c>nfm_reply</c>) <c>response_json</c>. Supports both the legacy flat schema
///     (<c>event_slug</c>, <c>start_time</c>, <c>duration</c>) and the v7.3 appointment template schema
///     (<c>service_slug</c>, <c>start_time_iso</c>, <c>duration_minutes</c>).
/// </summary>
public sealed class WhatsAppBookingFlowResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public string? EventSlug { get; private init; }
    public DateTimeOffset? StartTime { get; private init; }
    public int? DurationMinutes { get; private init; }
    public string? TimeZone { get; private init; }
    public string? BookerName { get; private init; }
    public string? BookerEmail { get; private init; }

    public static WhatsAppBookingFlowResponse? TryParse(string? responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // v7.3 appointment template schema: service_slug + start_time_iso + duration_minutes
            var serviceSluv7 = root.TryGetStringProp("service_slug");
            var startTimeIso = root.TryGetStringProp("start_time_iso");
            var durationV7 = root.TryGetIntProp("duration_minutes");
            var timezoneV7 = root.TryGetStringProp("timezone");
            var nameV7 = root.TryGetStringProp("booker_name");
            var emailV7 = root.TryGetStringProp("booker_email");

            if (!string.IsNullOrWhiteSpace(serviceSluv7) && !string.IsNullOrWhiteSpace(startTimeIso))
            {
                if (!DateTimeOffset.TryParse(startTimeIso, out var parsedStart)) return null;
                var response = new WhatsAppBookingFlowResponse
                {
                    EventSlug = serviceSluv7,
                    StartTime = parsedStart,
                    DurationMinutes = durationV7 > 0 ? durationV7 : null,
                    TimeZone = string.IsNullOrWhiteSpace(timezoneV7) ? "UTC" : timezoneV7,
                    BookerName = nameV7,
                    BookerEmail = emailV7
                };
                return response.IsBookable() ? response : null;
            }

            // Legacy flat schema: event_slug + start_time + duration
            var legacySlug = root.TryGetStringProp("event_slug");
            var legacyStartTime = root.TryGetStringProp("start_time");
            var legacyDuration = root.TryGetIntProp("duration");
            var legacyTimezone = root.TryGetStringProp("timezone");
            var legacyName = root.TryGetStringProp("booker_name");
            var legacyEmail = root.TryGetStringProp("booker_email");

            if (!string.IsNullOrWhiteSpace(legacySlug) && !string.IsNullOrWhiteSpace(legacyStartTime))
            {
                if (!DateTimeOffset.TryParse(legacyStartTime, out var parsedStart)) return null;
                var response = new WhatsAppBookingFlowResponse
                {
                    EventSlug = legacySlug,
                    StartTime = parsedStart,
                    DurationMinutes = legacyDuration > 0 ? legacyDuration : null,
                    TimeZone = string.IsNullOrWhiteSpace(legacyTimezone) ? "UTC" : legacyTimezone,
                    BookerName = legacyName,
                    BookerEmail = legacyEmail
                };
                return response.IsBookable() ? response : null;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

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

internal static class JsonElementDocExtensions
{
    public static string? TryGetStringProp(this JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    public static int TryGetIntProp(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return 0;
        if (p.TryGetInt32(out var i)) return i;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s)) return s;
        return 0;
    }
}


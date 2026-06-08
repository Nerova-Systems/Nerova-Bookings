using SharedKernel.Cqrs;
using System.Text.Json;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Queries;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using Main.Features.WhatsAppBooking.Domain;

namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     Handles decrypted Meta Flows data-exchange requests for the WhatsApp Booking Flow.
///     Services the APPOINTMENT screen''s cascading on-select dropdowns (service -> dates -> times)
///     and the DETAILS screen''s summary generation. The terminal SUMMARY screen data_exchange
///     is handled by the conversation engine via the nfm_reply webhook.
/// </summary>
public sealed class WhatsAppBookingFlowDataEndpoint(
    IWhatsAppConversationRepository conversationRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IEventTypeRepository eventTypeRepository,
    IMediator mediator,
    TimeProvider timeProvider,
    ILogger<WhatsAppBookingFlowDataEndpoint> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private const string FlowVersion = "3.0";
    // Default timezone — TODO: replace with per-tenant config when available.
    private const string DefaultTimezone = "Africa/Johannesburg";

    public async Task<string> HandleEncryptedAsync(
        WhatsAppFlowCrypto crypto,
        string encryptedAesKey, string encryptedFlowData, string initialVector,
        CancellationToken ct)
    {
        var decrypted = crypto.Decrypt(encryptedAesKey, encryptedFlowData, initialVector);
        var responseJson = await ProcessAsync(decrypted.Json, ct);
        return crypto.Encrypt(responseJson, decrypted.AesKey, decrypted.Iv);
    }

    private async Task<string> ProcessAsync(string requestJson, CancellationToken ct)
    {
        FlowDataRequest? req;
        try { req = JsonSerializer.Deserialize<FlowDataRequest>(requestJson, JsonOptions); }
        catch (JsonException ex) { logger.LogWarning(ex, "Failed to parse Booking Flow request"); return Ping(); }

        if (req is null || req.Action == "ping") return Ping();

        if (req.Action == "init") return await HandleInitAsync(req.FlowToken, ct);

        if (req.Screen == "APPOINTMENT")
        {
            var trigger = req.DataString("trigger");
            return trigger switch
            {
                "service_selected" => await HandleServiceSelectedAsync(req, ct),
                "date_selected" => await HandleDateSelectedAsync(req, ct),
                _ => Ping()
            };
        }

        if (req.Screen == "DETAILS") return await HandleDetailsAsync(req, ct);

        return Ping();
    }

    // -- Handlers -----------------------------------------------------------------

    private async Task<string> HandleInitAsync(string? flowToken, CancellationToken ct)
    {
        var profile = await ResolveProfileAsync(flowToken, ct);
        if (profile is null) return AppointmentScreen([], false, [], false, [], false, 0, DefaultTimezone);

        var services = await GetServicesAsync(profile, ct);
        return AppointmentScreen(services, false, [], false, [], false, 0, DefaultTimezone);
    }

    private async Task<string> HandleServiceSelectedAsync(FlowDataRequest req, CancellationToken ct)
    {
        var serviceSlug = req.DataString("service");
        if (string.IsNullOrWhiteSpace(serviceSlug)) return Ping();

        var profile = await ResolveProfileAsync(req.FlowToken, ct);
        if (profile is null) return Ping();

        var services = await GetServicesAsync(profile, ct);
        var eventType = (await eventTypeRepository.GetPublicListByOwnerUnfilteredAsync(profile.TenantId, profile.OwnerUserId, ct))
            .FirstOrDefault(et => et.Slug == serviceSlug);
        if (eventType is null) return AppointmentScreen(services, false, [], false, [], false, 0, DefaultTimezone);

        var now = timeProvider.GetUtcNow();
        var slotsResult = await mediator.Send(new GetPublicSlotsQuery(profile.Handle, serviceSlug, now, now.AddDays(30), DefaultTimezone), ct);

        var dates = BuildDates(slotsResult);
        return AppointmentScreen(services, true, dates, true, [], false, eventType.DurationMinutes, DefaultTimezone);
    }

    private async Task<string> HandleDateSelectedAsync(FlowDataRequest req, CancellationToken ct)
    {
        var serviceSlug = req.DataString("service");
        var dateStr = req.DataString("date");
        if (string.IsNullOrWhiteSpace(serviceSlug) || string.IsNullOrWhiteSpace(dateStr)) return Ping();

        var profile = await ResolveProfileAsync(req.FlowToken, ct);
        if (profile is null) return Ping();

        var allEventTypes = await eventTypeRepository.GetPublicListByOwnerUnfilteredAsync(profile.TenantId, profile.OwnerUserId, ct);
        var services = allEventTypes.Select(et => new FlowItem(et.Slug, et.Title)).ToArray();
        var eventType = allEventTypes.FirstOrDefault(et => et.Slug == serviceSlug);
        if (eventType is null) return AppointmentScreen(services, false, [], false, [], false, 0, DefaultTimezone);

        var now = timeProvider.GetUtcNow();

        // Dates: re-fetch full 30-day window to repopulate the date dropdown.
        var allSlotsResult = await mediator.Send(new GetPublicSlotsQuery(profile.Handle, serviceSlug, now, now.AddDays(30), DefaultTimezone), ct);
        var dates = BuildDates(allSlotsResult);

        // Times: fetch just the selected day.
        var date = DateOnly.Parse(dateStr);
        var dayStart = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
        var daySlotsResult = await mediator.Send(new GetPublicSlotsQuery(profile.Handle, serviceSlug, dayStart, dayStart.AddDays(1), DefaultTimezone), ct);

        var times = Array.Empty<FlowItem>();
        if (daySlotsResult.IsSuccess && daySlotsResult.Value?.Slots.TryGetValue(dateStr, out var daySlots) == true)
        {
            times = daySlots.Select(s =>
            {
                try
                {
                    var local = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(s.Time, DefaultTimezone);
                    return new FlowItem(s.Time.ToString("o"), local.ToString("HH:mm"));
                }
                catch
                {
                    return new FlowItem(s.Time.ToString("o"), s.Time.ToString("HH:mm"));
                }
            }).ToArray();
        }

        return AppointmentScreen(services, true, dates, true, times, times.Length > 0, eventType.DurationMinutes, DefaultTimezone);
    }

    private async Task<string> HandleDetailsAsync(FlowDataRequest req, CancellationToken ct)
    {
        var serviceSlug = req.DataString("service_slug") ?? string.Empty;
        var startTimeIso = req.DataString("start_time_iso") ?? string.Empty;
        var timezone = req.DataString("timezone") ?? DefaultTimezone;
        var name = req.DataString("name") ?? string.Empty;
        var email = req.DataString("email") ?? string.Empty;
        var durationMinutes = 30;
        if (req.Data.HasValue && req.Data.Value.TryGetProperty("duration_minutes", out var dm) && dm.TryGetInt32(out var v))
            durationMinutes = v;

        string summary;
        try
        {
            var start = DateTimeOffset.Parse(startTimeIso);
            var local = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(start, timezone);
            summary = $"Service: {serviceSlug}\n{local:dddd, d MMM yyyy} at {local:HH:mm} ({durationMinutes} min)\n\nName: {name}\nEmail: {email}";
        }
        catch
        {
            summary = $"{serviceSlug} — {startTimeIso}\n\nName: {name}\nEmail: {email}";
        }

        return SummaryScreen(summary, serviceSlug, startTimeIso, durationMinutes, timezone, name, email);
    }

    // -- Screen builders ----------------------------------------------------------

    private static string Ping() =>
        JsonSerializer.Serialize(new { version = FlowVersion, data = new { status = "active" } }, JsonOptions);

    private static string AppointmentScreen(FlowItem[] services, bool serviceEnabled, FlowItem[] dates,
        bool dateEnabled, FlowItem[] times, bool timeEnabled, int durationMinutes, string timezone)
    {
        return JsonSerializer.Serialize(new
        {
            version = FlowVersion, screen = "APPOINTMENT",
            data = new
            {
                service = services, date = dates, time = times,
                is_date_enabled = dateEnabled, is_time_enabled = timeEnabled,
                duration_minutes = durationMinutes, timezone
            }
        }, JsonOptions);
    }

    private static string SummaryScreen(string summary, string serviceSlug, string startTimeIso,
        int durationMinutes, string timezone, string name, string email)
    {
        return JsonSerializer.Serialize(new
        {
            version = FlowVersion, screen = "SUMMARY",
            data = new { summary, service_slug = serviceSlug, start_time_iso = startTimeIso, duration_minutes = durationMinutes, timezone, name, email }
        }, JsonOptions);
    }

    // -- Helpers ------------------------------------------------------------------

    private async Task<Main.Features.Scheduling.Domain.SchedulingProfile?> ResolveProfileAsync(string? flowToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(flowToken)) return null;
        WhatsAppConversationId conversationId;
        try { conversationId = new WhatsAppConversationId(flowToken); } catch { return null; }
        var conversation = await conversationRepository.GetByIdAsync(conversationId, ct);
        if (conversation is null) return null;
        return await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(conversation.TenantId, ct);
    }

    private async Task<FlowItem[]> GetServicesAsync(Main.Features.Scheduling.Domain.SchedulingProfile profile, CancellationToken ct)
    {
        var eventTypes = await eventTypeRepository.GetPublicListByOwnerUnfilteredAsync(profile.TenantId, profile.OwnerUserId, ct);
        return eventTypes.Select(et => new FlowItem(et.Slug, et.Title)).ToArray();
    }

    private static FlowItem[] BuildDates(Result<PublicSlotsResponse> result)
    {
        if (!result.IsSuccess || result.Value is null) return [];
        return result.Value.Slots.Keys.OrderBy(d => d)
            .Select(d => new FlowItem(d, DateOnly.Parse(d).ToString("ddd d MMM yyyy")))
            .ToArray();
    }
}
internal sealed record FlowItem(string Id, string Title);


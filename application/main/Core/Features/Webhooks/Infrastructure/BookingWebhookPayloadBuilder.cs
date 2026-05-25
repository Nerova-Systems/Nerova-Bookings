using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Webhooks.Domain;

namespace Main.Features.Webhooks.Infrastructure;

/// <summary>
///     Builds the JSON payload posted to webhook subscribers when a booking lifecycle event
///     fires. Mirrors the cal.com shape: a top-level envelope with <c>triggerEvent</c>,
///     <c>createdAt</c>, and a <c>payload</c> object carrying the booking snapshot. Kept as a
///     pure static helper so the serialised JSON can be locked down with golden-file tests and
///     reused from any caller (command handler today; background job tomorrow).
///     <para>
///         <b>Determinism.</b> Property order, casing (camelCase), and the
///         <c>DateTimeOffset</c> format (round-trip "O") are fixed by <see cref="JsonOptions" />.
///         Anything that changes the wire shape is a breaking change for subscribers and must be
///         caught by the snapshot tests.
///     </para>
/// </summary>
[PublicAPI]
public static class BookingWebhookPayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    ///     Builds the canonical envelope for a booking-lifecycle event. <paramref name="report" />
    ///     is populated only for <see cref="WebhookEventType.BookingReported" /> deliveries.
    /// </summary>
    public static string Build(
        WebhookEventType triggerEvent,
        DateTimeOffset createdAt,
        Booking booking,
        EventType? eventType,
        IReadOnlyList<BookingAttendee>? attendees = null,
        BookingReport? report = null
    )
    {
        var payload = new BookingWebhookPayload(
            Id: booking.Id.Value,
            Uid: booking.ICalUid ?? booking.Id.Value,
            Title: eventType?.Title ?? string.Empty,
            EventTypeSlug: eventType?.Slug,
            EventTypeId: eventType?.Id.Value,
            StartTime: booking.StartTime,
            EndTime: booking.EndTime,
            TimeZone: booking.TimeZone,
            Status: booking.Status.ToString(),
            LocationType: booking.LocationType,
            Location: booking.LocationValue,
            CancellationReason: booking.CancellationReason,
            Rescheduled: booking.Rescheduled ? true : null,
            ICalSequence: booking.ICalSequence,
            Organizer: new BookingWebhookOrganizer(
                Id: booking.OwnerUserId.Value,
                TenantId: booking.TenantId.Value
            ),
            Attendees: BuildAttendees(booking, attendees),
            Report: report is null
                ? null
                : new BookingWebhookReport(
                    Id: report.Id.Value,
                    ReasonCode: report.ReasonCode.ToString(),
                    Notes: report.Notes,
                    ReportedByUserId: report.ReportedByUserId.Value
                )
        );

        var envelope = new BookingWebhookEnvelope(
            TriggerEvent: triggerEvent,
            CreatedAt: createdAt,
            Payload: payload
        );

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private static IReadOnlyList<BookingWebhookAttendee> BuildAttendees(
        Booking booking,
        IReadOnlyList<BookingAttendee>? extras
    )
    {
        // The Booking aggregate carries the primary booker inline (BookerName/BookerEmail);
        // BookingAttendee is the side table for additional guests. Subscribers expect a single
        // flat list with the booker first so cal.com-style integrations keep working.
        var result = new List<BookingWebhookAttendee>(1 + (extras?.Count ?? 0))
        {
            new(
                Name: booking.BookerName,
                Email: booking.BookerEmail,
                TimeZone: booking.TimeZone,
                Locale: null
            )
        };

        if (extras is not null)
        {
            foreach (var attendee in extras)
            {
                // Skip duplicates of the booker (some flows persist the booker as a BookingAttendee
                // row too). Case-insensitive compare on email — that is the booker key cal.com uses.
                if (string.Equals(attendee.Email, booking.BookerEmail, StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new BookingWebhookAttendee(
                    Name: attendee.Name,
                    Email: attendee.Email,
                    TimeZone: attendee.TimeZone,
                    Locale: string.IsNullOrWhiteSpace(attendee.Locale) ? null : attendee.Locale
                ));
            }
        }

        return result;
    }
}

internal sealed record BookingWebhookEnvelope(
    WebhookEventType TriggerEvent,
    DateTimeOffset CreatedAt,
    BookingWebhookPayload Payload
);

internal sealed record BookingWebhookPayload(
    string Id,
    string Uid,
    string Title,
    string? EventTypeSlug,
    string? EventTypeId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string TimeZone,
    string Status,
    string? LocationType,
    string? Location,
    string? CancellationReason,
    bool? Rescheduled,
    int ICalSequence,
    BookingWebhookOrganizer Organizer,
    IReadOnlyList<BookingWebhookAttendee> Attendees,
    BookingWebhookReport? Report
);

internal sealed record BookingWebhookOrganizer(string Id, long TenantId);

internal sealed record BookingWebhookAttendee(string Name, string Email, string TimeZone, string? Locale);

internal sealed record BookingWebhookReport(string Id, string ReasonCode, string? Notes, string ReportedByUserId);

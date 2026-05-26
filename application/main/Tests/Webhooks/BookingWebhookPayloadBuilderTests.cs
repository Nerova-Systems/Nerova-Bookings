using System.Text.Json;
using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Infrastructure;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Webhooks;

/// <summary>
///     Locks down the wire shape of booking-lifecycle webhook payloads. The envelope keys,
///     casing, enum serialisation, and key ordering are part of the public contract — anything
///     that changes them is a breaking change for subscribers and must surface here.
/// </summary>
public sealed class BookingWebhookPayloadBuilderTests
{
    private static readonly DateTimeOffset FixedCreatedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FixedStart = new(2026, 6, 15, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Build_BookingCreated_ProducesExpectedEnvelope()
    {
        var booking = CreateBooking();

        var json = BookingWebhookPayloadBuilder.Build(
            WebhookEventType.BookingCreated,
            FixedCreatedAt,
            booking,
            null
        );

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("triggerEvent").GetString().Should().Be("bookingCreated");
        root.GetProperty("createdAt").GetString().Should().Be("2026-06-01T12:00:00+00:00");

        var payload = root.GetProperty("payload");
        payload.GetProperty("id").GetString().Should().Be(booking.Id.Value);
        payload.GetProperty("uid").GetString().Should().Be(booking.CalUid);
        payload.GetProperty("status").GetString().Should().Be("Accepted");
        payload.GetProperty("startTime").GetString().Should().Be("2026-06-15T14:30:00+00:00");
        payload.GetProperty("endTime").GetString().Should().Be("2026-06-15T15:00:00+00:00");
        payload.GetProperty("timeZone").GetString().Should().Be("UTC");
        payload.GetProperty("iCalSequence").GetInt32().Should().Be(0);

        var organizer = payload.GetProperty("organizer");
        organizer.GetProperty("id").GetString().Should().Be(booking.OwnerUserId.Value);
        organizer.GetProperty("tenantId").GetInt64().Should().Be(1);

        var attendees = payload.GetProperty("attendees");
        attendees.GetArrayLength().Should().Be(1);
        attendees[0].GetProperty("email").GetString().Should().Be("booker@example.com");
        attendees[0].GetProperty("name").GetString().Should().Be("Bob Booker");
    }

    [Fact]
    public void Build_NullEventType_OmitsSlugAndIdAndKeepsTitleEmpty()
    {
        var booking = CreateBooking();

        var json = BookingWebhookPayloadBuilder.Build(
            WebhookEventType.BookingCreated, FixedCreatedAt, booking, null
        );

        using var doc = JsonDocument.Parse(json);
        var payload = doc.RootElement.GetProperty("payload");
        payload.GetProperty("title").GetString().Should().Be(string.Empty);
        payload.TryGetProperty("eventTypeSlug", out _).Should().BeFalse();
        payload.TryGetProperty("eventTypeId", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_BookingReported_IncludesReportBlock()
    {
        var booking = CreateBooking();
        var report = BookingReport.Create(
            new TenantId(1), booking.Id, booking.OwnerUserId, BookingReportReasonCode.Spam, "  unwanted promo  "
        );

        var json = BookingWebhookPayloadBuilder.Build(
            WebhookEventType.BookingReported, FixedCreatedAt, booking, null, null, report
        );

        using var doc = JsonDocument.Parse(json);
        var reportNode = doc.RootElement.GetProperty("payload").GetProperty("report");
        reportNode.GetProperty("id").GetString().Should().Be(report.Id.Value);
        reportNode.GetProperty("reasonCode").GetString().Should().Be("Spam");
        reportNode.GetProperty("notes").GetString().Should().Be("unwanted promo");
        reportNode.GetProperty("reportedByUserId").GetString().Should().Be(booking.OwnerUserId.Value);
    }

    [Fact]
    public void Build_DeduplicatesBookerFromExtraAttendees()
    {
        var booking = CreateBooking();
        // BookingAttendee row carrying the same booker email — happens when flows persist the
        // booker as an attendee too. Builder must not emit them twice.
        var duplicate = BookingAttendee.Create(new TenantId(1), booking.Id, "Bob Booker", "BOOKER@example.com", "UTC", "en-US");
        var extra = BookingAttendee.Create(new TenantId(1), booking.Id, "Carol Guest", "carol@example.com", "Europe/Copenhagen", "da-DK");

        var json = BookingWebhookPayloadBuilder.Build(
            WebhookEventType.BookingCreated, FixedCreatedAt, booking, null, [duplicate, extra]
        );

        using var doc = JsonDocument.Parse(json);
        var attendees = doc.RootElement.GetProperty("payload").GetProperty("attendees");
        attendees.GetArrayLength().Should().Be(2);
        attendees[0].GetProperty("email").GetString().Should().Be("booker@example.com");
        attendees[1].GetProperty("email").GetString().Should().Be("carol@example.com");
        attendees[1].GetProperty("locale").GetString().Should().Be("da-DK");
    }

    [Fact]
    public void Build_IsDeterministic_ForSameInputs()
    {
        var booking = CreateBooking();

        var first = BookingWebhookPayloadBuilder.Build(
            WebhookEventType.BookingCreated, FixedCreatedAt, booking, null
        );
        var second = BookingWebhookPayloadBuilder.Build(
            WebhookEventType.BookingCreated, FixedCreatedAt, booking, null
        );

        first.Should().Be(second);
    }

    private static Booking CreateBooking()
    {
        return Booking.Create(
            new TenantId(1),
            new UserId("usr_01HV0000000000000000000000"),
            new EventTypeId("evt_test"),
            FixedStart,
            30,
            0,
            0,
            "Bob Booker",
            "booker@example.com",
            "UTC",
            BookingStatus.Accepted,
            new Dictionary<string, string>()
        );
    }
}

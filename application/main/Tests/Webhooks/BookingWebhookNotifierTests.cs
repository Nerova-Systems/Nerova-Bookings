using System.Text.Json;
using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Webhooks;

/// <summary>
///     Verifies the booking → webhook bridge contract:
///     <list type="bullet">
///         <item>Each lifecycle call delegates to <see cref="IWebhookDispatcher.FanOutAsync" /> exactly once.</item>
///         <item>The payload is built by <see cref="BookingWebhookPayloadBuilder" /> (envelope shape preserved).</item>
///         <item>Dispatcher failures are swallowed (best-effort guarantee).</item>
///     </list>
///     Fan-out behaviour (multi-subscriber, inactive-filtering) is covered by the dispatcher's own
///     tests — the notifier is a thin wrapper and does not duplicate that logic.
/// </summary>
public sealed class BookingWebhookNotifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IWebhookDispatcher _dispatcher = Substitute.For<IWebhookDispatcher>();
    private readonly TimeProvider _timeProvider = new FixedTimeProvider(Now);

    [Theory]
    [InlineData(WebhookEventType.BookingCreated)]
    [InlineData(WebhookEventType.BookingCancelled)]
    [InlineData(WebhookEventType.BookingRescheduled)]
    public async Task NotifyAsync_LifecycleEvent_CallsFanOutOnceWithBuiltPayload(WebhookEventType triggerEvent)
    {
        var notifier = CreateNotifier();
        var booking = CreateBooking();
        _dispatcher.FanOutAsync(booking.TenantId, triggerEvent, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<WebhookDeliveryId>()));

        await notifier.NotifyAsync(triggerEvent, booking, eventType: null, attendees: null, report: null, CancellationToken.None);

        await _dispatcher.Received(1).FanOutAsync(
            booking.TenantId,
            triggerEvent,
            Arg.Is<string>(json => PayloadHasTrigger(json, triggerEvent) && PayloadHasBookingId(json, booking.Id.Value)),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task NotifyAsync_BookingReported_IncludesReportBlockInPayload()
    {
        var notifier = CreateNotifier();
        var booking = CreateBooking();
        var report = BookingReport.Create(
            new TenantId(1), booking.Id, booking.OwnerUserId, BookingReportReasonCode.NoShow, "didn't show"
        );
        _dispatcher.FanOutAsync(booking.TenantId, WebhookEventType.BookingReported, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<WebhookDeliveryId>()));

        await notifier.NotifyAsync(
            WebhookEventType.BookingReported, booking, eventType: null, attendees: null, report: report, CancellationToken.None
        );

        await _dispatcher.Received(1).FanOutAsync(
            booking.TenantId,
            WebhookEventType.BookingReported,
            Arg.Is<string>(json => PayloadHasReportReason(json, "NoShow")),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task NotifyAsync_WhenDispatcherThrows_SwallowsAndDoesNotPropagate()
    {
        var notifier = CreateNotifier();
        var booking = CreateBooking();
        _dispatcher.FanOutAsync(Arg.Any<TenantId>(), Arg.Any<WebhookEventType>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("subscriber lookup blew up"));

        var act = () => notifier.NotifyAsync(
            WebhookEventType.BookingCreated, booking, eventType: null, attendees: null, report: null, CancellationToken.None
        );

        await act.Should().NotThrowAsync();
    }

    private BookingWebhookNotifier CreateNotifier()
    {
        return new BookingWebhookNotifier(_dispatcher, _timeProvider, NullLogger<BookingWebhookNotifier>.Instance);
    }

    private static Booking CreateBooking()
    {
        return Booking.Create(
            tenantId: new TenantId(1),
            ownerUserId: UserId.NewId(),
            eventTypeId: EventTypeId.NewId(),
            startTime: new DateTimeOffset(2026, 6, 15, 14, 30, 0, TimeSpan.Zero),
            durationMinutes: 30,
            beforeEventBufferMinutes: 0,
            afterEventBufferMinutes: 0,
            bookerName: "Bob Booker",
            bookerEmail: "booker@example.com",
            timeZone: "UTC",
            status: BookingStatus.Accepted,
            responses: new Dictionary<string, string>()
        );
    }

    private static bool PayloadHasTrigger(string json, WebhookEventType expected)
    {
        using var doc = JsonDocument.Parse(json);
        // Serialiser uses camelCase enum names — matches BookingWebhookPayloadBuilder JsonOptions.
        var triggerName = doc.RootElement.GetProperty("triggerEvent").GetString();
        return string.Equals(triggerName, JsonNamingPolicy.CamelCase.ConvertName(expected.ToString()), StringComparison.Ordinal);
    }

    private static bool PayloadHasBookingId(string json, string bookingId)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("payload").GetProperty("id").GetString() == bookingId;
    }

    private static bool PayloadHasReportReason(string json, string reason)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("payload").GetProperty("report").GetProperty("reasonCode").GetString() == reason;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

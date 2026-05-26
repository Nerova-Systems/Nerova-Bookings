using System.Text;
using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.EmailTemplates;
using Main.Features.Scheduling.Notifications;
using Main.Features.Workflows.Senders;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.Emails;
using SharedKernel.Integrations.Email;
using Xunit;

namespace Main.Tests.Scheduling;

/// <summary>
///     Pure unit tests for the booking notification dispatcher. Renderer and email client are
///     substituted; the dispatcher is exercised in isolation. Keeps the locale resolution and
///     null-host skip rules under explicit coverage so regressions surface fast.
/// </summary>
public sealed class BookingNotificationDispatcherTests
{
    private readonly IEmailClient _emailClient = Substitute.For<IEmailClient>();
    private readonly IEmailRenderer _emailRenderer = Substitute.For<IEmailRenderer>();
    private readonly IUserContactLookup _userContactLookup = Substitute.For<IUserContactLookup>();

    public BookingNotificationDispatcherTests()
    {
        _emailRenderer.RenderEmail(Arg.Any<EmailTemplateBase>())
            .Returns(call =>
                {
                    var template = call.Arg<EmailTemplateBase>();
                    return new EmailRenderResult($"subject-{template.Name}-{template.Locale}", "<html></html>", "text");
                }
            );
    }

    [Fact]
    public async Task DispatchAsync_Created_SendsAttendeeAndHost()
    {
        // Arrange
        var booking = CreateBooking();
        _userContactLookup.GetAsync(booking.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new UserContactInfo("host@example.com", "da-DK", "Anna Host"));

        var dispatcher = CreateDispatcher();

        // Act
        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Created, CancellationToken.None);

        // Assert: attendee gets en-US confirmation, host gets da-DK confirmation
        await _emailClient.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.Recipient == "booker@example.com" && m.Subject.EndsWith("en-US", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>()
        );
        await _emailClient.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.Recipient == "host@example.com" && m.Subject.EndsWith("da-DK", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task DispatchAsync_HostLookupReturnsNull_SkipsHostStillSendsAttendee()
    {
        var booking = CreateBooking();
        _userContactLookup.GetAsync(booking.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((UserContactInfo?)null);

        var dispatcher = CreateDispatcher();

        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Cancelled, CancellationToken.None);

        await _emailClient.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.Recipient == "booker@example.com"),
            Arg.Any<CancellationToken>()
        );
        // No second call (host)
        await _emailClient.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_HostBlankLocale_FallsBackToEnUs()
    {
        var booking = CreateBooking();
        _userContactLookup.GetAsync(booking.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new UserContactInfo("host@example.com", string.Empty, "Anna Host"));

        var dispatcher = CreateDispatcher();

        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Rescheduled, CancellationToken.None);

        // Host also gets en-US when locale is blank
        await _emailClient.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.Recipient == "host@example.com" && m.Subject.EndsWith("en-US", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task DispatchAsync_PicksTemplatePerKind()
    {
        var booking = CreateBooking();
        _userContactLookup.GetAsync(booking.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((UserContactInfo?)null); // attendee-only path

        var dispatcher = CreateDispatcher();

        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Created, CancellationToken.None);
        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Cancelled, CancellationToken.None);
        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Rescheduled, CancellationToken.None);

        _emailRenderer.Received(1).RenderEmail(Arg.Is<EmailTemplateBase>(t => t is BookingConfirmationEmailTemplate));
        _emailRenderer.Received(1).RenderEmail(Arg.Is<EmailTemplateBase>(t => t is BookingCancellationEmailTemplate));
        _emailRenderer.Received(1).RenderEmail(Arg.Is<EmailTemplateBase>(t => t is BookingRescheduleEmailTemplate));
    }

    [Fact]
    public async Task DispatchBookingConfirmation_AttachesIcsInvite()
    {
        var booking = CreateBooking();
        _userContactLookup.GetAsync(booking.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new UserContactInfo("host@example.com", "en-US", "Anna Host"));

        var dispatcher = CreateDispatcher();

        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Created, CancellationToken.None);

        await _emailClient.Received(2).SendAsync(
            Arg.Is<EmailMessage>(m => HasIcsInvite(m, "REQUEST", "CONFIRMED", booking.CalUid!, booking.CalSequence)),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task DispatchBookingCancellation_AttachesIcsCancellation()
    {
        var booking = CreateBooking();
        booking.Cancel("no longer needed");
        _userContactLookup.GetAsync(booking.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new UserContactInfo("host@example.com", "en-US", "Anna Host"));

        var dispatcher = CreateDispatcher();

        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Cancelled, CancellationToken.None);

        await _emailClient.Received(2).SendAsync(
            Arg.Is<EmailMessage>(m => HasIcsInvite(m, "CANCEL", "CANCELLED", booking.CalUid!, booking.CalSequence)),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task DispatchBookingReschedule_AttachesIcsUpdate()
    {
        var booking = CreateBooking();
        booking.MarkRescheduled("user-rescheduler");
        _userContactLookup.GetAsync(booking.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new UserContactInfo("host@example.com", "en-US", "Anna Host"));

        var dispatcher = CreateDispatcher();

        await dispatcher.DispatchAsync(booking, null, BookingNotificationKind.Rescheduled, CancellationToken.None);

        // CalSequence should be 1 after MarkRescheduled (was 0 on creation).
        booking.CalSequence.Should().Be(1);
        await _emailClient.Received(2).SendAsync(
            Arg.Is<EmailMessage>(m => HasIcsInvite(m, "REQUEST", "CONFIRMED", booking.CalUid!, booking.CalSequence)),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public void CalendarFileBuilder_Build_ProducesParseableIcsWithExpectedFields()
    {
        var booking = CreateBooking();

        var bytes = CalendarFileBuilder.Build(
            booking,
            CalendarMethod.Request,
            "30 min meeting",
            "host@example.com",
            "Anna Host"
        );

        var ics = Encoding.UTF8.GetString(bytes);
        var logicalLines = UnfoldIcs(ics);

        logicalLines.Should().Contain("BEGIN:VCALENDAR");
        logicalLines.Should().Contain("VERSION:2.0");
        logicalLines.Should().Contain("METHOD:REQUEST");
        logicalLines.Should().Contain("BEGIN:VEVENT");
        logicalLines.Should().Contain($"UID:{booking.CalUid}");
        logicalLines.Should().Contain($"SEQUENCE:{booking.CalSequence}");
        logicalLines.Should().Contain("STATUS:CONFIRMED");
        logicalLines.Should().Contain("SUMMARY:30 min meeting");
        logicalLines.Should().Contain("END:VEVENT");
        logicalLines.Should().Contain("END:VCALENDAR");
        // ORGANIZER + at least two ATTENDEE lines (host + booker)
        logicalLines.Should().Contain(l => l.StartsWith("ORGANIZER", StringComparison.Ordinal) && l.Contains("mailto:host@example.com"));
        logicalLines.Count(l => l.StartsWith("ATTENDEE", StringComparison.Ordinal)).Should().Be(2);
        // All lines end with CRLF in the raw output (no bare LF).
        ics.Should().NotContain("\n\n");
        ics.Replace("\r\n", string.Empty).Should().NotContain("\n");
    }

    private static bool HasIcsInvite(EmailMessage m, string method, string status, string uid, int sequence)
    {
        if (m.Enclosures is null || m.Enclosures.Count != 1) return false;
        var enclosure = m.Enclosures[0];
        if (enclosure.FileName != "invite.ics") return false;
        if (!enclosure.ContentType.StartsWith("text/calendar", StringComparison.Ordinal)) return false;

        var body = Encoding.UTF8.GetString(enclosure.ContentBytes);
        var lines = UnfoldIcs(body);
        return lines.Contains($"METHOD:{method}")
               && lines.Contains($"STATUS:{status}")
               && lines.Contains($"UID:{uid}")
               && lines.Contains($"SEQUENCE:{sequence}");
    }

    /// <summary>
    ///     Tiny RFC 5545 §3.1 unfolder: lines starting with a single space or tab are
    ///     continuations of the previous logical line.
    /// </summary>
    private static List<string> UnfoldIcs(string ics)
    {
        var rawLines = ics.Split("\r\n");
        var logical = new List<string>();
        foreach (var line in rawLines)
        {
            if (line.Length == 0) continue;
            if ((line[0] == ' ' || line[0] == '\t') && logical.Count > 0)
            {
                logical[^1] += line[1..];
            }
            else
            {
                logical.Add(line);
            }
        }

        return logical;
    }

    private BookingNotificationDispatcher CreateDispatcher()
    {
        return new BookingNotificationDispatcher(
            _userContactLookup,
            _emailRenderer,
            _emailClient,
            NullLogger<BookingNotificationDispatcher>.Instance
        );
    }

    private static Booking CreateBooking()
    {
        return Booking.Create(
            new TenantId(1),
            UserId.NewId(),
            new EventTypeId("evt_test"),
            DateTimeOffset.UtcNow.AddHours(2),
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

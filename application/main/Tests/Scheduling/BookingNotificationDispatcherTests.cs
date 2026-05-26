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

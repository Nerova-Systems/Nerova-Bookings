using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.EmailTemplates;
using Main.Features.Workflows.Senders;
using SharedKernel.Emails;
using SharedKernel.Integrations.Email;

namespace Main.Features.Scheduling.Notifications;

public enum BookingNotificationKind
{
    Created,
    Cancelled,
    Rescheduled
}

/// <summary>
///     Sends booking lifecycle emails (confirmation/cancellation/reschedule) to both the booker
///     (attendee) and the booking owner (host).
///     <para>
///         <b>Locale resolution.</b> Attendee gets <c>en-US</c> because <see cref="Booking" /> has
///         no locale field (deferred — extend the aggregate to carry the booker's locale).
///         Host gets the locale stored against their account user; missing/blank falls back to
///         <c>en-US</c>.
///     </para>
///     <para>
///         <b>Host lookup.</b> Delegates to <see cref="IUserContactLookup" /> which today resolves
///         against the account-database <c>users</c> table. If the lookup returns null (user
///         removed, cross-SCS connection unavailable) the host email is skipped — the attendee
///         email still goes out.
///     </para>
/// </summary>
public interface IBookingNotificationDispatcher
{
    Task DispatchAsync(Booking booking, EventType? eventType, BookingNotificationKind kind, CancellationToken ct);
}

public sealed class BookingNotificationDispatcher(
    IUserContactLookup userContactLookup,
    IEmailRenderer emailRenderer,
    IEmailClient emailClient,
    ILogger<BookingNotificationDispatcher> logger
) : IBookingNotificationDispatcher
{
    private const string DefaultLocale = "en-US";

    public async Task DispatchAsync(
        Booking booking,
        EventType? eventType,
        BookingNotificationKind kind,
        CancellationToken ct
    )
    {
        // Host contact resolves email + locale + display name in one cross-SCS hop. May be null
        // when the booking owner has been deleted or the account database is unreachable.
        var host = await userContactLookup.GetAsync(booking.OwnerUserId, ct);
        var hostName = host?.DisplayName ?? "your host";
        var eventTitle = eventType?.Title ?? "your meeting";

        // Build the calendar invite once per dispatch — the same .ics rides along both recipient
        // emails so the booker and host calendars stay in sync. When host contact lookup failed
        // (null) we still attach an invite for the booker; ORGANIZER falls back to a noreply
        // address since cal clients reject events with an empty ORGANIZER mailto.
        var calendarMethod = kind == BookingNotificationKind.Cancelled ? CalendarMethod.Cancel : CalendarMethod.Request;
        var icsBytes = CalendarFileBuilder.Build(
            booking,
            calendarMethod,
            eventTitle,
            host?.Email ?? "noreply@nerova",
            hostName
        );
        var enclosures = new[]
        {
            new EmailEnclosure(
                "invite.ics",
                $"text/calendar; method={(calendarMethod == CalendarMethod.Cancel ? "CANCEL" : "REQUEST")}; charset=utf-8",
                icsBytes
            )
        };

        // Attendee: always en-US (Booking has no locale field — see class doc).
        await SendAsync(
            kind,
            DefaultLocale,
            booking.BookerEmail,
            booking.BookerName,
            booking,
            hostName,
            eventTitle,
            enclosures,
            ct
        );

        // Host: send only when we know their email/locale. Skip silently when the lookup returned
        // null — this is normal for legacy/orphaned bookings.
        if (host is null)
        {
            logger.LogWarning(
                "Booking {BookingId} {Kind} notification skipped for host {UserId}: contact lookup returned null.",
                booking.Id, kind, booking.OwnerUserId
            );
            return;
        }

        await SendAsync(
            kind,
            ResolveLocale(host.Locale),
            host.Email,
            host.DisplayName,
            booking,
            hostName,
            eventTitle,
            enclosures,
            ct
        );
    }

    private async Task SendAsync(
        BookingNotificationKind kind,
        string locale,
        string toEmail,
        string recipientName,
        Booking booking,
        string hostName,
        string eventTitle,
        IReadOnlyList<EmailEnclosure> enclosures,
        CancellationToken ct
    )
    {
        var model = new BookingEmailModel(
            recipientName,
            booking.BookerName,
            hostName,
            eventTitle,
            booking.StartTime,
            booking.EndTime,
            booking.TimeZone,
            booking.LocationValue,
            booking.CancellationReason
        );

        EmailTemplateBase template = kind switch
        {
            BookingNotificationKind.Created => new BookingConfirmationEmailTemplate(locale, model),
            BookingNotificationKind.Cancelled => new BookingCancellationEmailTemplate(locale, model),
            BookingNotificationKind.Rescheduled => new BookingRescheduleEmailTemplate(locale, model),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        var rendered = emailRenderer.RenderEmail(template);
        await emailClient.SendAsync(
            new EmailMessage(toEmail, rendered.Subject, rendered.HtmlBody, rendered.PlainTextBody, Enclosures: enclosures),
            ct
        );
    }

    private static string ResolveLocale(string locale)
    {
        return string.IsNullOrWhiteSpace(locale) ? DefaultLocale : locale;
    }
}

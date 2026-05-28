using SharedKernel.Emails;

namespace Main.Features.Scheduling.EmailTemplates;

/// <summary>
///     Confirms a new booking. Sent to both the booker (attendee) and the booking owner (host).
///     The shared model is filled with the same data; the template renders attendee-focused vs.
///     host-focused copy based on the locale-specific file content.
/// </summary>
public sealed record BookingConfirmationEmailTemplate(string Locale, BookingEmailModel Data)
    : EmailTemplateBase("BookingConfirmation", Locale, Data);

/// <summary>Notifies attendee and host that a booking has been cancelled.</summary>
public sealed record BookingCancellationEmailTemplate(string Locale, BookingEmailModel Data)
    : EmailTemplateBase("BookingCancellation", Locale, Data);

/// <summary>Notifies attendee and host that a booking has been rescheduled to a new time.</summary>
public sealed record BookingRescheduleEmailTemplate(string Locale, BookingEmailModel Data)
    : EmailTemplateBase("BookingReschedule", Locale, Data);

/// <summary>
///     Shared model for all booking lifecycle templates. Fields cover the visible facts of a
///     booking; templates pick which subset they render. Times are formatted using the helper
///     <c>format_date</c> (see <c>EmailHelpers</c>).
/// </summary>
public sealed record BookingEmailModel(
    string RecipientName,
    string BookerName,
    string HostName,
    string EventTitle,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string TimeZone,
    string? Location,
    string? Reason
);

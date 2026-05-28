using System.Globalization;
using System.Text;
using Main.Features.Scheduling.Domain;

namespace Main.Features.Scheduling.Notifications;

/// <summary>
///     Indicates which iCalendar METHOD/STATUS pair to render for a booking lifecycle event.
/// </summary>
public enum CalendarMethod
{
    /// <summary>METHOD:REQUEST + STATUS:CONFIRMED. New invite or update (reschedule).</summary>
    Request,

    /// <summary>METHOD:CANCEL + STATUS:CANCELLED. Booking cancellation.</summary>
    Cancel
}

/// <summary>
///     Hand-written RFC 5545 ICS builder for booking lifecycle invites. Produces the bytes that
///     ride along the confirmation/cancellation/reschedule emails so calendar clients (Gmail,
///     Outlook, Apple Calendar) can one-click add/update/cancel the event.
///     <para>
///         No third-party dependencies — keeps the integration boundary thin and the surface area
///         testable in pure unit tests. Lines are folded at 75 octets per §3.1 and TEXT values
///         escaped per §3.3.11.
///     </para>
/// </summary>
public static class CalendarFileBuilder
{
    private const string ProductId = "-//Nerova Bookings//EN";
    private const string DateTimeFormat = "yyyyMMddTHHmmssZ";

    public static byte[] Build(
        Booking booking,
        CalendarMethod method,
        string summary,
        string hostEmail,
        string hostName
    )
    {
        var sb = new StringBuilder();
        AppendLine(sb, "BEGIN:VCALENDAR");
        AppendLine(sb, "VERSION:2.0");
        AppendLine(sb, $"PRODID:{ProductId}");
        AppendLine(sb, method == CalendarMethod.Cancel ? "METHOD:CANCEL" : "METHOD:REQUEST");
        AppendLine(sb, "CALSCALE:GREGORIAN");

        AppendLine(sb, "BEGIN:VEVENT");
        AppendLine(sb, $"UID:{booking.CalUid ?? $"{booking.Id.Value}@nerova"}");
        AppendLine(sb, $"SEQUENCE:{booking.CalSequence.ToString(CultureInfo.InvariantCulture)}");
        AppendLine(sb, $"DTSTAMP:{DateTime.UtcNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}");
        AppendLine(sb, $"DTSTART:{booking.StartTime.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}");
        AppendLine(sb, $"DTEND:{booking.EndTime.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}");
        AppendLine(sb, $"SUMMARY:{EscapeText(summary)}");
        AppendLine(sb, $"LOCATION:{EscapeText(booking.LocationValue ?? string.Empty)}");
        AppendLine(sb, $"DESCRIPTION:{EscapeText(string.Empty)}");
        AppendLine(sb, $"ORGANIZER;CN={EscapeText(hostName)}:mailto:{hostEmail}");

        // Host attendee: ACCEPTED on confirmation/reschedule (METHOD=REQUEST), NEEDS-ACTION on cancel.
        var hostPartStat = method == CalendarMethod.Cancel ? "NEEDS-ACTION" : "ACCEPTED";
        AppendLine(
            sb,
            $"ATTENDEE;CN={EscapeText(hostName)};RSVP=TRUE;PARTSTAT={hostPartStat}:mailto:{hostEmail}"
        );
        AppendLine(
            sb,
            $"ATTENDEE;CN={EscapeText(booking.BookerName)};RSVP=TRUE;PARTSTAT=NEEDS-ACTION:mailto:{booking.BookerEmail}"
        );

        AppendLine(sb, method == CalendarMethod.Cancel ? "STATUS:CANCELLED" : "STATUS:CONFIRMED");
        AppendLine(sb, "END:VEVENT");
        AppendLine(sb, "END:VCALENDAR");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Appends a logical content line, folding at 75 octets per RFC 5545 §3.1. Continuation
    ///     lines begin with a single space.
    /// </summary>
    private static void AppendLine(StringBuilder sb, string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        if (bytes.Length <= 75)
        {
            sb.Append(line).Append("\r\n");
            return;
        }

        // Split on octet boundaries: first chunk 75 bytes, subsequent chunks 74 bytes (leading space).
        var offset = 0;
        var first = true;
        while (offset < bytes.Length)
        {
            var chunkSize = first ? 75 : 74;
            var remaining = bytes.Length - offset;
            var take = Math.Min(chunkSize, remaining);
            // Avoid splitting a multi-byte UTF-8 codepoint by backing up to a leading-byte boundary.
            while (take > 0 && offset + take < bytes.Length && (bytes[offset + take] & 0xC0) == 0x80)
            {
                take--;
            }

            if (!first)
            {
                sb.Append(' ');
            }

            sb.Append(Encoding.UTF8.GetString(bytes, offset, take));
            sb.Append("\r\n");
            offset += take;
            first = false;
        }
    }

    /// <summary>Escapes TEXT values per RFC 5545 §3.3.11 (backslash, semicolon, comma, newline).</summary>
    private static string EscapeText(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    sb.Append(@"\\");
                    break;
                case ';':
                    sb.Append(@"\;");
                    break;
                case ',':
                    sb.Append(@"\,");
                    break;
                case '\r':
                    break;
                case '\n':
                    sb.Append(@"\n");
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        return sb.ToString();
    }
}

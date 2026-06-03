using Main.Features.Apps.Connectors.Office365Calendar;
using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.MsTeams;

/// <summary>
///     Microsoft Teams runs no OAuth flow of its own — it generates meeting links via Microsoft
///     Graph's <c>/me/onlineMeetings</c> endpoint, reusing the host's Office 365 Calendar
///     credential. It specifically requires <c>OnlineMeetings.ReadWrite</c> and relies on the
///     other Office 365 calendar scopes the shared credential carries. Scope strings are
///     referenced from <see cref="Office365CalendarOptions" /> for a single source of truth and
///     described in terms of the Office 365 connection Teams operates through.
/// </summary>
public static class MsTeamsPermissions
{
    public static readonly IReadOnlyList<AppPermission> All =
    [
        new(
            Office365CalendarOptions.OnlineMeetingsScope,
            "Manage Teams meetings",
            "Uses your Office 365 connection to create Microsoft Teams meeting links for your bookings."
        ),
        new(
            Office365CalendarOptions.OfflineAccessScope,
            "Stay connected",
            "Uses your Office 365 connection to refresh access in the background without repeated sign-in."
        ),
        new(
            Office365CalendarOptions.CalendarsReadWriteScope,
            "Read and manage calendars",
            "Uses your Office 365 Calendar connection to read availability and manage booking events."
        )
    ];
}

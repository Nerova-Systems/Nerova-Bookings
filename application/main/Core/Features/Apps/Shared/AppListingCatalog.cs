using Main.Features.Apps.Connectors.GoogleCalendar;
using Main.Features.Apps.Connectors.GoogleMeet;
using Main.Features.Apps.Connectors.MsTeams;
using Main.Features.Apps.Connectors.Office365Calendar;
using Main.Features.Apps.Connectors.Zoom;
using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Shared;

/// <summary>
///     Static marketing metadata for the App Store listing / detail pages — the publisher, pricing,
///     contact details, a longer overview, and screenshot URLs surfaced through
///     <see cref="AppResponse" />. Mirrors cal.com's per-connector <c>_metadata.ts</c> + the
///     <c>DESCRIPTION.md</c> frontmatter, but is centralised here so the response layer can resolve a
///     listing by slug without touching the persisted <see cref="App" /> registry rows. Slugs without
///     an entry fall back to a neutral default so the API never returns null metadata.
/// </summary>
public static class AppListingCatalog
{
    private static readonly IReadOnlyDictionary<string, AppListing> Listings = new Dictionary<string, AppListing>
    {
        [GoogleCalendarSlug.Value] = new AppListing(
            "Google",
            "Free",
            "https://workspace.google.com/products/calendar/",
            "support@google.com",
            "Google Calendar is the time-management and scheduling calendar service from Google. Connect it to keep your bookings in sync, automatically push new events to your calendar, and respect existing busy time so attendees can only book when you are genuinely free.",
            ["/app-screenshots/google-calendar-1.svg", "/app-screenshots/google-calendar-2.svg"]
        ),
        [Office365CalendarSlug.Value] = new AppListing(
            "Microsoft",
            "Free",
            "https://www.microsoft.com/microsoft-365/outlook/calendar",
            "support@microsoft.com",
            "Office 365 Calendar keeps your Outlook and Microsoft 365 schedule in sync with your bookings. New events are written back to your calendar and existing meetings block out availability so you are never double-booked.",
            ["/app-screenshots/office365-calendar-1.svg", "/app-screenshots/office365-calendar-2.svg"]
        ),
        [ZoomSlug.Value] = new AppListing(
            "Zoom Video Communications",
            "Free",
            "https://zoom.us/",
            "support@zoom.us",
            "Zoom is the video conferencing platform for meetings and webinars. Connect it to automatically generate a unique Zoom meeting link for every confirmed booking and include it in confirmations and reminders.",
            ["/app-screenshots/zoom-1.svg", "/app-screenshots/zoom-2.svg"]
        ),
        [GoogleMeetSlug.Value] = new AppListing(
            "Google",
            "Free",
            "https://meet.google.com/",
            "support@google.com",
            "Google Meet adds a video conferencing link to your bookings using your connected Google Calendar. A Meet link is created for each event automatically — no extra sign-in required once Google Calendar is connected.",
            ["/app-screenshots/google-meet-1.svg", "/app-screenshots/google-meet-2.svg"]
        ),
        [MsTeamsSlug.Value] = new AppListing(
            "Microsoft",
            "Free",
            "https://www.microsoft.com/microsoft-teams/",
            "support@microsoft.com",
            "Microsoft Teams adds an online meeting link to your bookings using your connected Office 365 Calendar. A Teams meeting is generated for each event automatically once Office 365 Calendar is connected.",
            ["/app-screenshots/ms-teams-1.svg", "/app-screenshots/ms-teams-2.svg"]
        ),
        ["whatsapp"] = new AppListing(
            "Meta",
            "Free",
            "https://business.whatsapp.com/",
            "support@whatsapp.com",
            "WhatsApp Business lets you reach customers on the channel they already use. Configure your business profile, run Meta Embedded Signup, and trigger pre-built booking workflows over WhatsApp.",
            []
        )
    };

    public static AppListing For(AppSlug slug)
    {
        return Listings.GetValueOrDefault(slug.Value, AppListing.Default);
    }
}

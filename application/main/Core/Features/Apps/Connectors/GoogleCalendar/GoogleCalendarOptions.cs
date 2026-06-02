namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>
///     OAuth client credentials and endpoint configuration for the Google Calendar connector.
///     Bound from environment variables at startup. In development the values may be empty —
///     the installer returns a 412 "not configured" response so the rest of the app still boots.
/// </summary>
public sealed class GoogleCalendarOptions
{
    /// <summary>
    ///     Read-only access to the user's calendars and events — used for free/busy lookups so
    ///     bookings never double-book an existing event.
    /// </summary>
    public const string CalendarReadonlyScope = "https://www.googleapis.com/auth/calendar.readonly";

    /// <summary>
    ///     Read/write access to calendar events — used to create, update, and cancel the events
    ///     backing confirmed bookings (and to attach Google Meet conferencing).
    /// </summary>
    public const string CalendarEventsScope = "https://www.googleapis.com/auth/calendar.events";

    /// <summary>OAuth 2.0 client id (from Google Cloud Console → Credentials).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret (from Google Cloud Console → Credentials).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>OAuth authorize endpoint.</summary>
    public string AuthorizeUrl { get; init; } = "https://accounts.google.com/o/oauth2/v2/auth";

    /// <summary>OAuth token endpoint (code exchange + refresh).</summary>
    public string TokenUrl { get; init; } = "https://oauth2.googleapis.com/token";

    /// <summary>OAuth token revocation endpoint.</summary>
    public string RevokeUrl { get; init; } = "https://oauth2.googleapis.com/revoke";

    /// <summary>Google Calendar API base URL.</summary>
    public string ApiBaseUrl { get; init; } = "https://www.googleapis.com/calendar/v3";

    /// <summary>Scopes requested at authorize time.</summary>
    public string[] Scopes { get; set; } =
    [
        CalendarReadonlyScope,
        CalendarEventsScope
    ];
}

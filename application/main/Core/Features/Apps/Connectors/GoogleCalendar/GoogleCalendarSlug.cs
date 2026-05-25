using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>Shared constants for the Google Calendar connector.</summary>
public static class GoogleCalendarSlug
{
    public const string Value = "google-calendar";

    public static readonly AppSlug Slug = new(Value);

    public const string HttpClientName = "google-calendar";
}

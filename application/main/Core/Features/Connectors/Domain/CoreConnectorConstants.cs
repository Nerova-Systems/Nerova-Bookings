namespace Main.Features.Connectors.Domain;

public static class CoreConnectorConstants
{
    public const string GoogleCalendar = "google-calendar";
    public const string Office365Calendar = "office365-calendar";
    public const string GoogleMeet = "google-meet";
    public const string Office365Video = "office365-video";
    public const string ZoomVideo = "zoom-video";

    public static bool IsCoreCalendar(string integration)
    {
        return integration.Equals(GoogleCalendar, StringComparison.OrdinalIgnoreCase) ||
               integration.Equals(Office365Calendar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCoreConferencing(string app)
    {
        return app.Equals(GoogleMeet, StringComparison.OrdinalIgnoreCase) ||
               app.Equals(Office365Video, StringComparison.OrdinalIgnoreCase) ||
               app.Equals(ZoomVideo, StringComparison.OrdinalIgnoreCase);
    }
}

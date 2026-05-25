using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.GoogleMeet;

/// <summary>Shared constants for the Google Meet connector.</summary>
public static class GoogleMeetSlug
{
    public const string Value = "google-meet";

    public static readonly AppSlug Slug = new(Value);
}

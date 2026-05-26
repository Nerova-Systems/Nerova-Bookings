using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.Zoom;

/// <summary>Shared constants for the Zoom conferencing connector.</summary>
public static class ZoomSlug
{
    public const string Value = "zoom";

    public const string HttpClientName = "zoom";

    public static readonly AppSlug Slug = new(Value);
}

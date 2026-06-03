using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.Zoom;

/// <summary>
///     The real Zoom scope the connector relies on, surfaced through the Apps API. The scope
///     string is referenced from <see cref="ZoomOptions" /> so the descriptor and the documented
///     required Marketplace scope share a single source of truth.
/// </summary>
public static class ZoomPermissions
{
    public static readonly IReadOnlyList<AppPermission> All =
    [
        new(
            ZoomOptions.MeetingWriteScope,
            "Create Zoom meetings",
            "Creates and deletes Zoom meetings on your behalf to generate join links for your bookings."
        )
    ];
}

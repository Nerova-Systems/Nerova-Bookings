using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.MsTeams;

/// <summary>Shared constants for the Microsoft Teams conferencing connector.</summary>
public static class MsTeamsSlug
{
    public const string Value = "ms-teams";

    public static readonly AppSlug Slug = new(Value);
}

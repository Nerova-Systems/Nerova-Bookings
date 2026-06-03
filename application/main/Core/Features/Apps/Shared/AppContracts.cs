using JetBrains.Annotations;
using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Shared;

/// <summary>
///     Marketing metadata used to render the App Store listing and detail pages: the connector's
///     publisher, pricing label, contact details, a longer overview, and screenshot image URLs.
///     Resolved per-slug by <see cref="AppListingCatalog" />.
/// </summary>
[PublicAPI]
public sealed record AppListing(
    string Publisher,
    string Pricing,
    string Website,
    string SupportEmail,
    string Overview,
    string[] Screenshots,
    bool IsNew = false
)
{
    public static readonly AppListing Default = new(string.Empty, "Free", string.Empty, string.Empty, string.Empty, []);
}

[PublicAPI]
public sealed record AppResponse(
    AppSlug Slug,
    string Name,
    AppCategory Category,
    string Description,
    string LogoUrl,
    bool IsActive,
    bool IsInstalledForTenant,
    bool IsConnectedForUser,
    AppPermission[] Permissions,
    string Publisher,
    string Pricing,
    string Website,
    string SupportEmail,
    string Overview,
    string[] Screenshots,
    bool IsNew
)
{
    public static AppResponse From(
        App app,
        bool isInstalledForTenant,
        bool isConnectedForUser,
        AppPermission[] permissions,
        AppListing listing
    )
    {
        return new AppResponse(
            app.Id,
            app.Name,
            app.Category,
            app.Description,
            app.LogoUrl,
            app.IsActive,
            isInstalledForTenant,
            isConnectedForUser,
            permissions,
            listing.Publisher,
            listing.Pricing,
            listing.Website,
            listing.SupportEmail,
            string.IsNullOrWhiteSpace(listing.Overview) ? app.Description : listing.Overview,
            listing.Screenshots,
            listing.IsNew
        );
    }
}

[PublicAPI]
public sealed record AppsResponse(AppResponse[] Apps);

[PublicAPI]
public sealed record InstallAppResponse(string AuthorizeUrl, string State);

[PublicAPI]
public sealed record AppCallbackResponse(AppSlug Slug, bool Connected);

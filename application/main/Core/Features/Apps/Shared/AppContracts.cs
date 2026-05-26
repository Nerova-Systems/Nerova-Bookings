using JetBrains.Annotations;
using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Shared;

[PublicAPI]
public sealed record AppResponse(
    AppSlug Slug,
    string Name,
    AppCategory Category,
    string Description,
    string LogoUrl,
    bool IsActive,
    bool IsInstalledForTenant,
    bool IsConnectedForUser
)
{
    public static AppResponse From(App app, bool isInstalledForTenant, bool isConnectedForUser)
    {
        return new AppResponse(
            app.Id,
            app.Name,
            app.Category,
            app.Description,
            app.LogoUrl,
            app.IsActive,
            isInstalledForTenant,
            isConnectedForUser
        );
    }
}

[PublicAPI]
public sealed record AppsResponse(AppResponse[] Apps);

[PublicAPI]
public sealed record InstallAppResponse(string AuthorizeUrl, string State);

[PublicAPI]
public sealed record AppCallbackResponse(AppSlug Slug, bool Connected);

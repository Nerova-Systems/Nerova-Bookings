using JetBrains.Annotations;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Shared;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Apps.Queries;

[PublicAPI]
[RequirePermission(PermissionResource.App, PermissionAction.Read)]
public sealed record ListAppsQuery : IRequest<Result<AppsResponse>>;

public sealed class ListAppsHandler(
    IAppRepository appRepository,
    IAppInstallationRepository installationRepository,
    ICredentialRepository credentialRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListAppsQuery, Result<AppsResponse>>
{
    public async Task<Result<AppsResponse>> Handle(ListAppsQuery query, CancellationToken cancellationToken)
    {
        _ = query;

        var userId = executionContext.UserInfo.Id;
        if (userId is null) return Result<AppsResponse>.Unauthorized("Authentication is required.");

        var apps = await appRepository.GetAllAsync(cancellationToken);
        var installations = await installationRepository.GetForTenantAsync(cancellationToken);
        var userCredentials = await credentialRepository.GetForUserAsync(userId, cancellationToken);

        var installedSlugs = installations.Select(installation => installation.AppSlug).ToHashSet();
        var connectedSlugs = userCredentials.Select(credential => credential.AppSlug).ToHashSet();

        var responses = apps
            .Select(app => AppResponse.From(app, installedSlugs.Contains(app.Id), connectedSlugs.Contains(app.Id)))
            .ToArray();

        return new AppsResponse(responses);
    }
}

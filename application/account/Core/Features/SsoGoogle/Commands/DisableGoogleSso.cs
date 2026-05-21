using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoGoogle.Commands;

/// <summary>
///     Disables the Google SSO configuration for the active organization.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record DisableGoogleSsoCommand : ICommand, IRequest<Result>;

public sealed class DisableGoogleSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    IExecutionContext executionContext
) : IRequestHandler<DisableGoogleSsoCommand, Result>
{
    public async Task<Result> Handle(DisableGoogleSsoCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapSsoGoogle.Key))
        {
            return Result.Forbidden("The Google SSO feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var config = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Google, cancellationToken);
        if (config is null)
        {
            return Result.NotFound("No Google SSO configuration found for this organization.");
        }

        if (!config.IsEnabled)
        {
            return Result.Success();
        }

        config.Disable();
        ssoConfigRepository.Update(config);

        return Result.Success();
    }
}

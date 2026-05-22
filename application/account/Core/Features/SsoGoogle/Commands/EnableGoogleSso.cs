using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoGoogle.Commands;

/// <summary>
///     Enables the Google SSO configuration for the active organization.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record EnableGoogleSsoCommand : ICommand, IRequest<Result>;

public sealed class EnableGoogleSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    IExecutionContext executionContext
) : IRequestHandler<EnableGoogleSsoCommand, Result>
{
    public async Task<Result> Handle(EnableGoogleSsoCommand command, CancellationToken cancellationToken)
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

        if (config.IsEnabled)
        {
            return Result.Success();
        }

        config.Enable();
        ssoConfigRepository.Update(config);

        return Result.Success();
    }
}

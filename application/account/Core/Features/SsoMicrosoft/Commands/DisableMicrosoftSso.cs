using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoMicrosoft.Commands;

/// <summary>
///     Disables the Microsoft SSO configuration for the active organization.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record DisableMicrosoftSsoCommand : ICommand, IRequest<Result>;

public sealed class DisableMicrosoftSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    IExecutionContext executionContext
) : IRequestHandler<DisableMicrosoftSsoCommand, Result>
{
    public async Task<Result> Handle(DisableMicrosoftSsoCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapSsoMicrosoft.Key))
        {
            return Result.Forbidden("The Microsoft SSO feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var config = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Microsoft, cancellationToken);
        if (config is null)
        {
            return Result.NotFound("No Microsoft SSO configuration found for this organization.");
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

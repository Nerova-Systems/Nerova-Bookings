using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using Account.Features.SsoMicrosoft.Infrastructure;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoMicrosoft.Queries;

[PublicAPI]
public sealed record OrgMicrosoftSsoConfigResponse(
    OrgSsoConfigId Id,
    string AzureTenantId,
    string ClientId,
    string[] AllowedDomains,
    bool IsEnabled
);

/// <summary>
///     Returns the Microsoft SSO configuration for the active organization. The client secret is
///     never included in the response.
///     Requires <c>OrgSettings.Read</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Read, PermissionScope.Organization)]
public sealed record GetOrgMicrosoftSsoConfigQuery : IRequest<Result<OrgMicrosoftSsoConfigResponse>>;

public sealed class GetOrgMicrosoftSsoConfigHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    MicrosoftSsoConfigurator configurator,
    IExecutionContext executionContext
) : IRequestHandler<GetOrgMicrosoftSsoConfigQuery, Result<OrgMicrosoftSsoConfigResponse>>
{
    public async Task<Result<OrgMicrosoftSsoConfigResponse>> Handle(GetOrgMicrosoftSsoConfigQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapSsoMicrosoft.Key))
        {
            return Result<OrgMicrosoftSsoConfigResponse>.Forbidden("The Microsoft SSO feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var config = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Microsoft, cancellationToken);
        if (config is null)
        {
            return Result<OrgMicrosoftSsoConfigResponse>.NotFound("No Microsoft SSO configuration found for this organization.");
        }

        var resolved = configurator.DecryptConfig(config);
        if (resolved is null)
        {
            return Result<OrgMicrosoftSsoConfigResponse>.NotFound("Unable to decrypt the Microsoft SSO configuration.");
        }

        return new OrgMicrosoftSsoConfigResponse(
            config.Id,
            resolved.AzureTenantId,
            resolved.ClientId,
            resolved.AllowedDomains,
            config.IsEnabled
        );
    }
}

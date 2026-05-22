using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using Account.Features.SsoGoogle.Infrastructure;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoGoogle.Queries;

[PublicAPI]
public sealed record OrgGoogleSsoConfigResponse(
    OrgSsoConfigId Id,
    string HostedDomain,
    string ClientId,
    string[] AllowedDomains,
    bool IsEnabled
);

/// <summary>
///     Returns the Google SSO configuration for the active organization. The client secret is
///     never included in the response.
///     Requires <c>OrgSettings.Read</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Read, PermissionScope.Organization)]
public sealed record GetOrgGoogleSsoConfigQuery : IRequest<Result<OrgGoogleSsoConfigResponse>>;

public sealed class GetOrgGoogleSsoConfigHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    GoogleSsoConfigurator configurator,
    IExecutionContext executionContext
) : IRequestHandler<GetOrgGoogleSsoConfigQuery, Result<OrgGoogleSsoConfigResponse>>
{
    public async Task<Result<OrgGoogleSsoConfigResponse>> Handle(GetOrgGoogleSsoConfigQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapSsoGoogle.Key))
        {
            return Result<OrgGoogleSsoConfigResponse>.Forbidden("The Google SSO feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var config = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Google, cancellationToken);
        if (config is null)
        {
            return Result<OrgGoogleSsoConfigResponse>.NotFound("No Google SSO configuration found for this organization.");
        }

        var resolved = configurator.DecryptConfig(config);
        if (resolved is null)
        {
            return Result<OrgGoogleSsoConfigResponse>.NotFound("Unable to decrypt the Google SSO configuration.");
        }

        return new OrgGoogleSsoConfigResponse(
            config.Id,
            resolved.HostedDomain,
            resolved.ClientId,
            resolved.AllowedDomains,
            config.IsEnabled
        );
    }
}

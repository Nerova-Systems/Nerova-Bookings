using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using Account.Features.SsoMicrosoft.Infrastructure;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.OpenIdConnect;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoMicrosoft.Commands;

[PublicAPI]
public sealed record TestMicrosoftSsoResult(bool Success, string? ErrorMessage);

/// <summary>
///     Tests connectivity to the configured Azure AD tenant by fetching the OIDC discovery document.
///     Validates that the <c>AzureTenantId</c> resolves to a reachable Microsoft identity endpoint.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record TestMicrosoftSsoCommand : ICommand, IRequest<Result<TestMicrosoftSsoResult>>;

public sealed class TestMicrosoftSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    MicrosoftSsoConfigurator configurator,
    OpenIdConnectConfigurationManagerFactory configManagerFactory,
    IExecutionContext executionContext
) : IRequestHandler<TestMicrosoftSsoCommand, Result<TestMicrosoftSsoResult>>
{
    public async Task<Result<TestMicrosoftSsoResult>> Handle(TestMicrosoftSsoCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapSsoMicrosoft.Key))
        {
            return Result<TestMicrosoftSsoResult>.Forbidden("The Microsoft SSO feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var config = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Microsoft, cancellationToken);
        if (config is null)
        {
            return Result<TestMicrosoftSsoResult>.NotFound("No Microsoft SSO configuration found for this organization.");
        }

        var resolved = configurator.DecryptConfig(config);
        if (resolved is null)
        {
            return Result<TestMicrosoftSsoResult>.NotFound("Unable to decrypt the Microsoft SSO configuration.");
        }

        try
        {
            var discoveryUrl = MicrosoftSsoConfigurator.GetDiscoveryUrl(resolved.AzureTenantId);
            var configManager = configManagerFactory.GetOrCreate(discoveryUrl);
            await configManager.GetConfigurationAsync(cancellationToken);

            return new TestMicrosoftSsoResult(true, null);
        }
        catch (Exception ex)
        {
            return new TestMicrosoftSsoResult(false, ex.Message);
        }
    }
}

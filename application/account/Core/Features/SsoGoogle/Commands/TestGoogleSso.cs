using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using Account.Features.SsoGoogle.Infrastructure;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.OpenIdConnect;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoGoogle.Commands;

[PublicAPI]
public sealed record TestGoogleSsoResult(bool Success, string? ErrorMessage);

/// <summary>
///     Tests connectivity to Google's OIDC endpoint and validates the stored configuration
///     by fetching the Google OIDC discovery document.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record TestGoogleSsoCommand : ICommand, IRequest<Result<TestGoogleSsoResult>>;

public sealed class TestGoogleSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    GoogleSsoConfigurator configurator,
    OpenIdConnectConfigurationManagerFactory configManagerFactory,
    IExecutionContext executionContext
) : IRequestHandler<TestGoogleSsoCommand, Result<TestGoogleSsoResult>>
{
    public async Task<Result<TestGoogleSsoResult>> Handle(TestGoogleSsoCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapSsoGoogle.Key))
        {
            return Result<TestGoogleSsoResult>.Forbidden("The Google SSO feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var config = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Google, cancellationToken);
        if (config is null)
        {
            return Result<TestGoogleSsoResult>.NotFound("No Google SSO configuration found for this organization.");
        }

        var resolved = configurator.DecryptConfig(config);
        if (resolved is null)
        {
            return Result<TestGoogleSsoResult>.NotFound("Unable to decrypt the Google SSO configuration.");
        }

        try
        {
            var configManager = configManagerFactory.GetOrCreate(GoogleSsoConfigurator.GoogleDiscoveryUrl);
            await configManager.GetConfigurationAsync(cancellationToken);

            return new TestGoogleSsoResult(true, null);
        }
        catch (Exception ex)
        {
            return new TestGoogleSsoResult(false, ex.Message);
        }
    }
}

using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using Account.Features.SsoGoogle.Infrastructure;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoGoogle.Commands;

/// <summary>
///     Upserts the Google Workspace SSO configuration for the active organization.
///     Creates a new config if none exists; updates the existing one otherwise.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record ConfigureGoogleSsoCommand : ICommand, IRequest<Result>
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    /// <summary>
    ///     Google Workspace hosted domain (e.g. "acme.com"). Only users from this domain
    ///     will be permitted to sign in via this SSO config.
    /// </summary>
    public required string HostedDomain { get; init; }

    public required string[] AllowedDomains { get; init; }
}

public sealed class ConfigureGoogleSsoValidator : AbstractValidator<ConfigureGoogleSsoCommand>
{
    public ConfigureGoogleSsoValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty()
            .MaximumLength(512)
            .WithMessage("Client ID is required.");

        RuleFor(x => x.ClientSecret)
            .NotEmpty()
            .MaximumLength(512)
            .WithMessage("Client Secret is required.");

        RuleFor(x => x.HostedDomain)
            .NotEmpty()
            .MaximumLength(253)
            .Matches(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z]{2,})+$")
            .WithMessage("Hosted domain must be a valid domain name (e.g. acme.com).");

        RuleFor(x => x.AllowedDomains)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one allowed domain is required.");

        RuleForEach(x => x.AllowedDomains)
            .NotEmpty()
            .MaximumLength(253)
            .Matches(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z]{2,})+$")
            .WithMessage("Each entry must be a valid domain name.");
    }
}

public sealed class ConfigureGoogleSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    ITenantRepository tenantRepository,
    GoogleSsoConfigurator configurator,
    IExecutionContext executionContext
) : IRequestHandler<ConfigureGoogleSsoCommand, Result>
{
    public async Task<Result> Handle(ConfigureGoogleSsoCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapSsoGoogle.Key))
        {
            return Result.Forbidden("The Google SSO feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var encryptedConfig = configurator.EncryptConfig(command.ClientId, command.ClientSecret, command.HostedDomain);

        var existing = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Google, cancellationToken);
        if (existing is not null)
        {
            existing.Update(encryptedConfig, command.AllowedDomains);
            ssoConfigRepository.Update(existing);
        }
        else
        {
            var tenant = await tenantRepository.GetByIdAsync(orgId, cancellationToken);
            if (tenant is null)
            {
                return Result.NotFound("Organization not found.");
            }

            var config = OrgSsoConfig.Create(tenant, SsoProvider.Google, encryptedConfig, command.AllowedDomains);
            await ssoConfigRepository.AddAsync(config, cancellationToken);
        }

        return Result.Success();
    }
}

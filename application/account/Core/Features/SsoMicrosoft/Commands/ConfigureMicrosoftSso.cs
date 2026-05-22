using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Sso.Domain;
using Account.Features.SsoMicrosoft.Infrastructure;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.SsoMicrosoft.Commands;

/// <summary>
///     Upserts the Microsoft SSO configuration for the active organization.
///     Creates a new config if none exists; updates the existing one otherwise.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record ConfigureMicrosoftSsoCommand : ICommand, IRequest<Result>
{
    public required string AzureTenantId { get; init; }

    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    public required string[] AllowedDomains { get; init; }
}

public sealed class ConfigureMicrosoftSsoValidator : AbstractValidator<ConfigureMicrosoftSsoCommand>
{
    public ConfigureMicrosoftSsoValidator()
    {
        RuleFor(x => x.AzureTenantId)
            .NotEmpty()
            .MaximumLength(36)
            .WithMessage("Azure Tenant ID must be between 1 and 36 characters.");

        RuleFor(x => x.ClientId)
            .NotEmpty()
            .MaximumLength(36)
            .WithMessage("Client ID must be between 1 and 36 characters.");

        RuleFor(x => x.ClientSecret)
            .NotEmpty()
            .MaximumLength(512)
            .WithMessage("Client Secret is required.");

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

public sealed class ConfigureMicrosoftSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    ITenantRepository tenantRepository,
    MicrosoftSsoConfigurator configurator,
    IExecutionContext executionContext
) : IRequestHandler<ConfigureMicrosoftSsoCommand, Result>
{
    public async Task<Result> Handle(ConfigureMicrosoftSsoCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapSsoMicrosoft.Key))
        {
            return Result.Forbidden("The Microsoft SSO feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var encryptedConfig = configurator.EncryptConfig(command.AzureTenantId, command.ClientId, command.ClientSecret);

        var existing = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Microsoft, cancellationToken);
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

            var config = OrgSsoConfig.Create(tenant, SsoProvider.Microsoft, encryptedConfig, command.AllowedDomains);
            await ssoConfigRepository.AddAsync(config, cancellationToken);
        }

        return Result.Success();
    }
}

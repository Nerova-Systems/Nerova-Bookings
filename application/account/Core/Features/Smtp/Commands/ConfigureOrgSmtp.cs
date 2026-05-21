using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Smtp.Domain;
using Account.Features.Smtp.Infrastructure;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Smtp.Commands;

/// <summary>
///     Upserts the SMTP configuration for the active organization.
///     Creates a new config if none exists; updates the existing one otherwise.
///     Requires <c>Smtp.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Smtp, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record ConfigureOrgSmtpCommand : ICommand, IRequest<Result>
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required bool UseSsl { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string FromEmail { get; init; }
    public string? FromName { get; init; }
    public string? ReplyToEmail { get; init; }
}

public sealed class ConfigureOrgSmtpValidator : AbstractValidator<ConfigureOrgSmtpCommand>
{
    public ConfigureOrgSmtpValidator()
    {
        RuleFor(x => x.Host)
            .NotEmpty()
            .MaximumLength(253)
            .WithMessage("Host must be between 1 and 253 characters.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535)
            .WithMessage("Port must be between 1 and 65535.");

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("Username must be between 1 and 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required.");

        RuleFor(x => x.FromEmail)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("A valid From email address is required.");

        RuleFor(x => x.ReplyToEmail)
            .EmailAddress()
            .When(x => x.ReplyToEmail is not null)
            .WithMessage("Reply-to must be a valid email address.");
    }
}

public sealed class ConfigureOrgSmtpHandler(
    IOrgSmtpConfigRepository configRepository,
    ITenantRepository tenantRepository,
    SmtpCredentialProtector credentialProtector,
    IExecutionContext executionContext) : IRequestHandler<ConfigureOrgSmtpCommand, Result>
{
    public async Task<Result> Handle(ConfigureOrgSmtpCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapCustomSmtp.Key))
            return Result.Forbidden("The custom SMTP feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;
        var encryptedPassword = credentialProtector.Protect(command.Password);

        var existing = await configRepository.GetByOrgIdAsync(orgId, cancellationToken);
        if (existing is not null)
        {
            existing.Update(
                command.Host,
                command.Port,
                command.UseSsl,
                command.Username,
                encryptedPassword,
                command.FromEmail,
                command.FromName,
                command.ReplyToEmail);

            configRepository.Update(existing);
        }
        else
        {
            var tenant = await tenantRepository.GetByIdAsync(orgId, cancellationToken);
            if (tenant is null)
                return Result.NotFound($"Organization '{orgId}' not found.");

            var config = OrgSmtpConfig.Create(
                tenant,
                command.Host,
                command.Port,
                command.UseSsl,
                command.Username,
                encryptedPassword,
                command.FromEmail,
                command.FromName,
                command.ReplyToEmail);

            await configRepository.AddAsync(config, cancellationToken);
        }

        return Result.Success();
    }
}

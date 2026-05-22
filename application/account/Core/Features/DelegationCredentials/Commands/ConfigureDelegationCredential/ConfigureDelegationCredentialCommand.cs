using Account.Features.AuditLog.Domain;
using Account.Features.DelegationCredentials.Domain;
using Account.Features.DelegationCredentials.Infrastructure;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.DelegationCredentials;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.DelegationCredentials.Commands.ConfigureDelegationCredential;

/// <summary>
///     Upserts a delegation credential for the active organization.
///     Creates a new credential if none exists for <see cref="Platform" />; rotates the key if one does.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record ConfigureDelegationCredentialCommand : ICommand, IRequest<Result>
{
    public required WorkspacePlatform Platform { get; init; }

    /// <summary>
    ///     The email domain this credential covers (e.g. <c>acme.com</c>).
    ///     Must not contain an <c>@</c> symbol.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    ///     The raw key blob — a Google service-account JSON string or a Microsoft OAuth refresh token.
    ///     Encrypted at rest before persistence.
    /// </summary>
    public required string KeyBlob { get; init; }
}

public sealed class ConfigureDelegationCredentialValidator : AbstractValidator<ConfigureDelegationCredentialCommand>
{
    public ConfigureDelegationCredentialValidator()
    {
        RuleFor(x => x.Domain)
            .NotEmpty()
            .MaximumLength(253)
            .Must(d => !d.Contains('@'))
            .WithMessage("Domain must not contain '@'. Provide the domain part only (e.g. 'acme.com').")
            .Must(d => d.Contains('.'))
            .WithMessage("Domain must be a valid domain name (e.g. 'acme.com').");

        RuleFor(x => x.KeyBlob)
            .NotEmpty()
            .WithMessage("Key blob is required.");
    }
}

public sealed class ConfigureDelegationCredentialHandler(
    IDelegationCredentialRepository credentialRepository,
    ITenantRepository tenantRepository,
    DelegationCredentialEncryption encryption,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext
) : IRequestHandler<ConfigureDelegationCredentialCommand, Result>
{
    public async Task<Result> Handle(ConfigureDelegationCredentialCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapDelegationCredentials.Key))
        {
            return Result.Forbidden("The delegation credentials feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var userId = executionContext.UserInfo.Id!;
        var encryptedKeyBlob = encryption.Protect(command.KeyBlob);

        var existing = await credentialRepository.GetByOrgAndPlatformAsync(orgId, command.Platform, cancellationToken);
        AuditAction auditAction;

        if (existing is not null)
        {
            existing.RotateKey(encryptedKeyBlob, command.Domain);
            credentialRepository.Update(existing);
            auditAction = AuditAction.KeyRotated;

            await EmitAuditAsync(auditAction, existing.Id.ToString(), orgId, userId, cancellationToken);
        }
        else
        {
            var tenant = await tenantRepository.GetByIdAsync(orgId, cancellationToken);
            if (tenant is null)
            {
                return Result.NotFound($"Organization '{orgId}' not found.");
            }

            var credential = DelegationCredential.Create(tenant, command.Platform, command.Domain, encryptedKeyBlob, userId);
            await credentialRepository.AddAsync(credential, cancellationToken);
            auditAction = AuditAction.Configured;

            await EmitAuditAsync(auditAction, credential.Id.ToString(), orgId, userId, cancellationToken);
        }

        return Result.Success();
    }

    private Task EmitAuditAsync(
        AuditAction action,
        string resourceId,
        TenantId orgId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        return auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                userId,
                executionContext.UserInfo.Email ?? string.Empty,
                AuditResource.DelegationCredential.ToString(),
                action.ToString(),
                resourceId,
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );
    }
}

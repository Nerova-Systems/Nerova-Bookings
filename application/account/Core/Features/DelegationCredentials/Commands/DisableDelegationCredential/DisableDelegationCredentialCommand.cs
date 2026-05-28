using Account.Features.AuditLog.Domain;
using Account.Features.DelegationCredentials.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.DelegationCredentials;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.DelegationCredentials.Commands.DisableDelegationCredential;

/// <summary>
///     Disables the delegation credential for <see cref="Platform" /> without deleting it.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record DisableDelegationCredentialCommand : ICommand, IRequest<Result>
{
    public required WorkspacePlatform Platform { get; init; }
}

public sealed class DisableDelegationCredentialHandler(
    IDelegationCredentialRepository credentialRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext
) : IRequestHandler<DisableDelegationCredentialCommand, Result>
{
    public async Task<Result> Handle(DisableDelegationCredentialCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapDelegationCredentials.Key))
        {
            return Result.Forbidden("The delegation credentials feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var credential = await credentialRepository.GetByOrgAndPlatformAsync(orgId, command.Platform, cancellationToken);
        if (credential is null)
        {
            return Result.NotFound($"No {command.Platform} delegation credential found for this organization.");
        }

        credential.Disable();
        credentialRepository.Update(credential);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.DelegationCredential),
                nameof(AuditAction.Disabled),
                credential.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        return Result.Success();
    }
}

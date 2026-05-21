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

namespace Account.Features.DelegationCredentials.Commands.DeleteDelegationCredential;

/// <summary>
///     Permanently removes the delegation credential for <see cref="Platform" />.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record DeleteDelegationCredentialCommand : ICommand, IRequest<Result>
{
    public required WorkspacePlatform Platform { get; init; }
}

public sealed class DeleteDelegationCredentialHandler(
    IDelegationCredentialRepository credentialRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext
) : IRequestHandler<DeleteDelegationCredentialCommand, Result>
{
    public async Task<Result> Handle(DeleteDelegationCredentialCommand command, CancellationToken cancellationToken)
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

        var credentialId = credential.Id.ToString();
        credentialRepository.Remove(credential);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                AuditResource.DelegationCredential.ToString(),
                AuditAction.Deleted.ToString(),
                credentialId,
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        return Result.Success();
    }
}

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

namespace Account.Features.DelegationCredentials.Commands.EnableDelegationCredential;

/// <summary>
///     Enables the delegation credential for <see cref="Platform" />.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record EnableDelegationCredentialCommand : ICommand, IRequest<Result>
{
    public required WorkspacePlatform Platform { get; init; }
}

public sealed class EnableDelegationCredentialHandler(
    IDelegationCredentialRepository credentialRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext
) : IRequestHandler<EnableDelegationCredentialCommand, Result>
{
    public async Task<Result> Handle(EnableDelegationCredentialCommand command, CancellationToken cancellationToken)
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

        credential.Enable();
        credentialRepository.Update(credential);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                AuditResource.DelegationCredential.ToString(),
                AuditAction.Enabled.ToString(),
                credential.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        return Result.Success();
    }
}

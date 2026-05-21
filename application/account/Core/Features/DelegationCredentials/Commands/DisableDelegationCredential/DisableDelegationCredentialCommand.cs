using Account.Features.AuditLog.Domain;
using Account.Features.DelegationCredentials.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Tenants.Domain;
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
    IExecutionContext executionContext) : IRequestHandler<DisableDelegationCredentialCommand, Result>
{
    public async Task<Result> Handle(DisableDelegationCredentialCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapDelegationCredentials.Key))
            return Result.Forbidden("The delegation credentials feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;
        var credential = await credentialRepository.GetByOrgAndPlatformAsync(orgId, command.Platform, cancellationToken);
        if (credential is null)
            return Result.NotFound($"No {command.Platform} delegation credential found for this organization.");

        credential.Disable();
        credentialRepository.Update(credential);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: executionContext.UserInfo.Id!,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.DelegationCredential.ToString(),
            Action: AuditAction.Disabled.ToString(),
            ResourceId: credential.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        return Result.Success();
    }
}

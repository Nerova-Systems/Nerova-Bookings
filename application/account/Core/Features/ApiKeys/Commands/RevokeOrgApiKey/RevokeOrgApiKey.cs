using Account.Features.AuditLog.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.ApiKeys.Domain;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.ApiKeys.Commands.RevokeOrgApiKey;

[PublicAPI]
[RequirePermission(PermissionResource.ApiKey, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record RevokeOrgApiKeyCommand(ApiKeyId Id) : ICommand, IRequest<Result>;

public sealed class RevokeOrgApiKeyHandler(
    IApiKeyRepository apiKeyRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<RevokeOrgApiKeyCommand, Result>
{
    public async Task<Result> Handle(RevokeOrgApiKeyCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapApiKeys.Key))
            return Result.Forbidden("The API keys feature is not enabled for this tenant.");

        var orgId = executionContext.ActiveOrgId!;

        // Bypass the tenant filter because the key lives in the org tenant, not the caller's solo tenant.
        var apiKey = await apiKeyRepository.GetByIdUnfilteredAsync(command.Id, cancellationToken);
        if (apiKey is null || apiKey.Scope != ApiKeyScope.Organization || apiKey.TenantId != orgId)
            return Result.NotFound($"API key '{command.Id}' not found in this organization.");

        if (apiKey.RevokedAt.HasValue)
            return Result.BadRequest("This API key has already been revoked.");

        apiKey.Revoke(timeProvider.GetUtcNow());
        apiKeyRepository.Update(apiKey);

        var userId = executionContext.UserInfo.Id!;
        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: userId,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.ApiKey.ToString(),
            Action: AuditAction.Revoked.ToString(),
            ResourceId: apiKey.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        return Result.Success();
    }
}

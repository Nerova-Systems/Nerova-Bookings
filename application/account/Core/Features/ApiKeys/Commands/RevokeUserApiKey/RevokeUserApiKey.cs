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

namespace Account.Features.ApiKeys.Commands.RevokeUserApiKey;

[PublicAPI]
[RequirePermission(PermissionResource.ApiKey, PermissionAction.Manage)]
public sealed record RevokeUserApiKeyCommand(ApiKeyId Id) : ICommand, IRequest<Result>;

public sealed class RevokeUserApiKeyHandler(
    IApiKeyRepository apiKeyRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<RevokeUserApiKeyCommand, Result>
{
    public async Task<Result> Handle(RevokeUserApiKeyCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapApiKeys.Key))
            return Result.Forbidden("The API keys feature is not enabled for this tenant.");

        // GetByIdAsync uses the normal tenant filter which scopes to the user's solo tenant.
        var apiKey = await apiKeyRepository.GetByIdAsync(command.Id, cancellationToken);
        if (apiKey is null || apiKey.Scope != ApiKeyScope.User)
            return Result.NotFound($"API key '{command.Id}' not found.");

        if (apiKey.RevokedAt.HasValue)
            return Result.BadRequest("This API key has already been revoked.");

        apiKey.Revoke(timeProvider.GetUtcNow());
        apiKeyRepository.Update(apiKey);

        var userId = executionContext.UserInfo.Id!;
        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: executionContext.TenantId!,
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

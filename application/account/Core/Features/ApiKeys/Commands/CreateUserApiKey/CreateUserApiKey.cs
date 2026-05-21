using Account.Features.ApiKeys.Domain;
using Account.Features.AuditLog.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.ApiKeys.Commands.CreateUserApiKey;

/// <summary>Response payload for a successful API key creation. The <see cref="PlainText" /> is shown once.</summary>
[PublicAPI]
public sealed record CreateApiKeyResponse(ApiKeyId Id, string PlainText, string KeyPrefix);

[PublicAPI]
[RequirePermission(PermissionResource.ApiKey, PermissionAction.Manage)]
public sealed record CreateUserApiKeyCommand : ICommand, IRequest<Result<CreateApiKeyResponse>>
{
    public required string Name { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class CreateUserApiKeyValidator : AbstractValidator<CreateUserApiKeyCommand>
{
    public CreateUserApiKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Name must be between 1 and 100 characters.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("Expiry must be in the future.");
    }
}

public sealed class CreateUserApiKeyHandler(
    IApiKeyRepository apiKeyRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext
) : IRequestHandler<CreateUserApiKeyCommand, Result<CreateApiKeyResponse>>
{
    public async Task<Result<CreateApiKeyResponse>> Handle(CreateUserApiKeyCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapApiKeys.Key))
        {
            return Result<CreateApiKeyResponse>.Forbidden("The API keys feature is not enabled for this tenant.");
        }

        var userId = executionContext.UserInfo.Id!;
        var tenantId = executionContext.TenantId!;

        var (apiKey, plainText) = ApiKey.CreateUserKey(tenantId, userId, command.Name, command.ExpiresAt);
        await apiKeyRepository.AddAsync(apiKey, cancellationToken);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                tenantId,
                userId,
                executionContext.UserInfo.Email ?? string.Empty,
                AuditResource.ApiKey.ToString(),
                AuditAction.Created.ToString(),
                apiKey.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        return new CreateApiKeyResponse(apiKey.Id, plainText, apiKey.KeyPrefix);
    }
}

using Account.Features.ApiKeys.Commands.CreateUserApiKey;
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

namespace Account.Features.ApiKeys.Commands.CreateOrgApiKey;

[PublicAPI]
[RequirePermission(PermissionResource.ApiKey, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record CreateOrgApiKeyCommand : ICommand, IRequest<Result<CreateApiKeyResponse>>
{
    public required string Name { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class CreateOrgApiKeyValidator : AbstractValidator<CreateOrgApiKeyCommand>
{
    public CreateOrgApiKeyValidator()
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

public sealed class CreateOrgApiKeyHandler(
    IApiKeyRepository apiKeyRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext
) : IRequestHandler<CreateOrgApiKeyCommand, Result<CreateApiKeyResponse>>
{
    public async Task<Result<CreateApiKeyResponse>> Handle(CreateOrgApiKeyCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapApiKeys.Key))
        {
            return Result<CreateApiKeyResponse>.Forbidden("The API keys feature is not enabled for this tenant.");
        }

        var userId = executionContext.UserInfo.Id!;
        var orgId = executionContext.ActiveOrgId!;

        var (apiKey, plainText) = ApiKey.CreateOrgKey(orgId, userId, command.Name, command.ExpiresAt);
        await apiKeyRepository.AddAsync(apiKey, cancellationToken);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                userId,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.ApiKey),
                nameof(AuditAction.Created),
                apiKey.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        return new CreateApiKeyResponse(apiKey.Id, plainText, apiKey.KeyPrefix);
    }
}

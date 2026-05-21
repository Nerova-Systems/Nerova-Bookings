using System.Security.Claims;
using Account.Features.Users.Domain;
using Account.Features.Users.Shared;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Authentication.TokenGeneration;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Impersonation.Commands;

[PublicAPI]
public sealed record StartBackOfficeImpersonationCommand : ICommand, IRequest<Result>
{
    [JsonIgnore]
    public UserId TargetUserId { get; init; } = null!;
}

public sealed class StartBackOfficeImpersonationHandler(
    IUserRepository userRepository,
    UserInfoFactory userInfoFactory,
    AuthenticationTokenService authenticationTokenService,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<StartBackOfficeImpersonationCommand, Result>
{
    private const string BackOfficeIdentifier = "backoffice";

    public async Task<Result> Handle(StartBackOfficeImpersonationCommand command, CancellationToken cancellationToken)
    {
        var targetUser = await userRepository.GetByIdUnfilteredAsync(command.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return Result.NotFound($"User '{command.TargetUserId}' not found.");
        }

        var userInfoResult = await userInfoFactory.CreateUserInfoAsync(
            targetUser,
            null,
            cancellationToken,
            impersonatedByIdentifier: BackOfficeIdentifier
        );
        if (!userInfoResult.IsSuccess) return Result.From(userInfoResult);

        authenticationTokenService.SetImpersonationAccessToken(userInfoResult.Value!);

        var adminEmail = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                targetUser.TenantId,
                null,
                adminEmail,
                "User",
                "ImpersonationStarted",
                command.TargetUserId.ToString(),
                new Dictionary<string, string>
                {
                    ["target_user_id"] = command.TargetUserId.ToString(),
                    ["original_actor_identifier"] = BackOfficeIdentifier
                },
                executionContext.ClientIpAddress.ToString(),
                httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty
            ), cancellationToken
        );

        events.CollectEvent(new BackOfficeImpersonationStarted(command.TargetUserId));

        return Result.Success();
    }
}

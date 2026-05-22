using Account.Features.Users.Domain;
using Account.Features.Users.Shared;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Authentication.TokenGeneration;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Impersonation.Commands;

[PublicAPI]
public sealed record EndImpersonationCommand : ICommand, IRequest<Result>;

public sealed class EndImpersonationHandler(
    IUserRepository userRepository,
    UserInfoFactory userInfoFactory,
    AuthenticationTokenService authenticationTokenService,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<EndImpersonationCommand, Result>
{
    public async Task<Result> Handle(EndImpersonationCommand command, CancellationToken cancellationToken)
    {
        var impersonatedByIdentifier = executionContext.UserInfo.ImpersonatedByIdentifier;
        if (impersonatedByIdentifier is null)
        {
            return Result.BadRequest("Current session is not an impersonated session.");
        }

        var targetUserId = executionContext.UserInfo.Id!;
        var impersonatedByUserId = executionContext.UserInfo.ImpersonatedByUserId;

        // BackOffice path — no org-admin token to restore; emit audit trail and return success
        if (impersonatedByUserId is null)
        {
            await auditLogEmitter.EmitAsync(new AuditLogEvent(
                    executionContext.TenantId!,
                    null,
                    impersonatedByIdentifier,
                    "User",
                    "ImpersonationEnded",
                    targetUserId.ToString(),
                    new Dictionary<string, string>
                    {
                        ["target_user_id"] = targetUserId.ToString(),
                        ["original_actor_identifier"] = impersonatedByIdentifier
                    },
                    executionContext.ClientIpAddress.ToString(),
                    httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty
                ), cancellationToken
            );

            events.CollectEvent(new BackOfficeImpersonationEnded(targetUserId));
            return Result.Success();
        }

        // Org-admin path — restore the original actor's token
        var actorUser = await userRepository.GetByIdUnfilteredAsync(impersonatedByUserId, cancellationToken);
        if (actorUser is null)
        {
            return Result.NotFound($"Original actor user '{impersonatedByUserId}' not found.");
        }

        var userInfoResult = await userInfoFactory.CreateUserInfoAsync(
            actorUser,
            executionContext.UserInfo.SessionId,
            cancellationToken,
            activeOrgId: executionContext.ActiveOrgId
        );
        if (!userInfoResult.IsSuccess) return Result.From(userInfoResult);

        authenticationTokenService.SetImpersonationAccessToken(userInfoResult.Value!);

        var activeOrgId = executionContext.ActiveOrgId ?? actorUser.TenantId;

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                activeOrgId,
                impersonatedByUserId,
                actorUser.Email,
                "User",
                "ImpersonationEnded",
                targetUserId.ToString(),
                new Dictionary<string, string>
                {
                    ["target_user_id"] = targetUserId.ToString(),
                    ["original_actor_user_id"] = impersonatedByUserId.ToString()
                },
                executionContext.ClientIpAddress.ToString(),
                httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty
            ), cancellationToken
        );

        events.CollectEvent(new ImpersonationEnded(impersonatedByUserId, targetUserId, activeOrgId));

        return Result.Success();
    }
}

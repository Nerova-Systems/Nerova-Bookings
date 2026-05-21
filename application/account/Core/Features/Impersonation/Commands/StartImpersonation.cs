using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
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
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Impersonation.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.User, PermissionAction.Impersonate, PermissionScope.Organization)]
public sealed record StartImpersonationCommand(UserId TargetUserId) : ICommand, IRequest<Result>;

public sealed class StartImpersonationHandler(
    IUserRepository userRepository,
    IMembershipRepository membershipRepository,
    UserInfoFactory userInfoFactory,
    AuthenticationTokenService authenticationTokenService,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<StartImpersonationCommand, Result>
{
    public async Task<Result> Handle(StartImpersonationCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapImpersonation.Key))
        {
            return Result.Forbidden("The impersonation feature is not enabled for this tenant.");
        }

        var activeOrgId = executionContext.ActiveOrgId;

        var targetUser = await userRepository.GetByIdUnfilteredAsync(command.TargetUserId, cancellationToken);
        if (targetUser is null || targetUser.TenantId != activeOrgId)
        {
            return Result.NotFound($"User '{command.TargetUserId}' not found in this organization.");
        }

        var membership = await membershipRepository.GetByUserAndTenantAsync(command.TargetUserId, activeOrgId!, cancellationToken);
        if (membership is null)
        {
            return Result.NotFound($"User '{command.TargetUserId}' is not a member of this organization.");
        }

        if (membership.DisableImpersonation)
        {
            return Result.Forbidden("This user has opted out of impersonation by organization admins.");
        }

        var actorId = executionContext.UserInfo.Id!;

        // Load actor from DB so email reflects the latest value rather than a potentially stale JWT claim
        var actorUser = await userRepository.GetByIdUnfilteredAsync(actorId, cancellationToken);
        var actorEmail = actorUser?.Email ?? executionContext.UserInfo.Email ?? string.Empty;

        var userInfoResult = await userInfoFactory.CreateUserInfoAsync(
            targetUser,
            sessionId: executionContext.UserInfo.SessionId,
            cancellationToken,
            activeOrgId: activeOrgId,
            impersonatedByIdentifier: actorId.ToString(),
            impersonatedByUserId: actorId
        );
        if (!userInfoResult.IsSuccess) return Result.From(userInfoResult);

        authenticationTokenService.SetImpersonationAccessToken(userInfoResult.Value!);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: activeOrgId!,
            ActorId: actorId,
            ActorEmail: actorEmail,
            Resource: "User",
            Action: "ImpersonationStarted",
            ResourceId: command.TargetUserId.ToString(),
            Metadata: new Dictionary<string, string>
            {
                ["target_user_id"] = command.TargetUserId.ToString(),
                ["original_actor_user_id"] = actorId.ToString()
            },
            IpAddress: executionContext.ClientIpAddress.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty
        ), cancellationToken);

        events.CollectEvent(new ImpersonationStarted(actorId, command.TargetUserId, activeOrgId!));

        return Result.Success();
    }
}

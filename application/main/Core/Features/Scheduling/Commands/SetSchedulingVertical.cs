using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Scheduling.Commands;

/// <summary>
///     Records the tenant's business vertical on the scheduling profile — the main-SCS copy of the
///     account tenant's vertical (docs/vertical-template-fields-spec.md §1). Welcome step 1 calls the
///     account command first and then this one, so clients, import, and agents can read the vertical
///     without a cross-SCS call.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Update)]
public sealed record SetSchedulingVerticalCommand(NerovaVertical Vertical) : ICommand, IRequest<Result>;

public sealed class SetSchedulingVerticalHandler(
    ISchedulingProfileRepository schedulingProfileRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<SetSchedulingVerticalCommand, Result>
{
    public async Task<Result> Handle(SetSchedulingVerticalCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (executionContext.TenantId is null || ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var profile = await schedulingProfileRepository.GetForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, cancellationToken);
        if (profile is null)
        {
            return Result.NotFound("No scheduling profile exists yet. Open the app once to create it, then retry.");
        }

        profile.SetVertical(command.Vertical);
        schedulingProfileRepository.Update(profile);

        events.CollectEvent(new SchedulingVerticalSet(command.Vertical.ToString()));

        return Result.Success();
    }
}

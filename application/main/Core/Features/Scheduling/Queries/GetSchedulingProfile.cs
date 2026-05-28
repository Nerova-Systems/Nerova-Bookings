using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Queries;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Read)]
public sealed record GetSchedulingProfileQuery : ICommand, IRequest<Result<SchedulingProfileResponse>>;

public sealed class GetSchedulingProfileHandler(ISchedulingProfileRepository schedulingProfileRepository, IExecutionContext executionContext)
    : IRequestHandler<GetSchedulingProfileQuery, Result<SchedulingProfileResponse>>
{
    public async Task<Result<SchedulingProfileResponse>> Handle(GetSchedulingProfileQuery query, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<SchedulingProfileResponse>.Unauthorized("Authentication is required.");
        }

        var profile = await schedulingProfileRepository.GetForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, cancellationToken);
        if (profile is not null)
        {
            return SchedulingProfileResponse.From(profile);
        }

        var baseHandle = SchedulingProfileHandle.DefaultFrom(executionContext.UserInfo);
        var handle = await GetAvailableHandle(baseHandle, ownerUserId, cancellationToken);
        var displayName = $"{executionContext.UserInfo.FirstName} {executionContext.UserInfo.LastName}".Trim();
        profile = SchedulingProfile.Create(
            tenantId,
            ownerUserId,
            handle,
            string.IsNullOrWhiteSpace(displayName) ? handle : displayName,
            executionContext.UserInfo.AvatarUrl,
            executionContext.ActiveTeamId
        );
        await schedulingProfileRepository.AddAsync(profile, cancellationToken);

        return SchedulingProfileResponse.From(profile);
    }

    private async Task<string> GetAvailableHandle(string baseHandle, UserId ownerUserId, CancellationToken cancellationToken)
    {
        var candidate = baseHandle;
        var suffix = 2;

        while (SchedulingProfileHandle.IsReserved(candidate) || await schedulingProfileRepository.HandleExistsAsync(candidate, ownerUserId, cancellationToken))
        {
            candidate = $"{baseHandle}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}

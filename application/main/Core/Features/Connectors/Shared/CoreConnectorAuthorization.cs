using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Features.Connectors.Shared;

public static class CoreConnectorAuthorization
{
    public static Result CanManageConnectors(IExecutionContext executionContext)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CapDelegationCredentials.Key))
        {
            return Result.Forbidden("Cal.com apps connectors are disabled for this tenant.");
        }

        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        if (executionContext.TenantId is null || executionContext.UserInfo.Id is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        return Result.Success();
    }

    public static Result CanManageConferencing(IExecutionContext executionContext)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CapDelegationCredentials.Key))
        {
            return Result.Forbidden("Cal.com conferencing is disabled for this tenant.");
        }

        return CanManageConnectors(executionContext);
    }

    public static async Task<Result<EventType>> GetOwnedEventTypeAsync(
        IEventTypeRepository eventTypeRepository,
        IExecutionContext executionContext,
        EventTypeId eventTypeId,
        CancellationToken cancellationToken
    )
    {
        var eventType = await eventTypeRepository.GetByIdAsync(eventTypeId, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != executionContext.UserInfo.Id)
        {
            return Result<EventType>.NotFound($"Event type '{eventTypeId}' was not found.");
        }

        return eventType;
    }
}

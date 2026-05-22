using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Features.BookingSideEffects.Shared;

public static class BookingSideEffectAuthorization
{
    public static Result CanManageWorkflows(IExecutionContext executionContext)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CapWorkflows.Key))
        {
            return Result.Forbidden("Cal.com workflows are disabled for this tenant.");
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

    public static Result CanManageWebhooks(IExecutionContext executionContext)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CapWorkflows.Key))
        {
            return Result.Forbidden("Cal.com webhooks are disabled for this tenant.");
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

    public static async Task<Result<EventType>> GetOwnedEventTypeAsync(IEventTypeRepository eventTypeRepository, IExecutionContext executionContext, EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<EventType>.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(eventTypeId, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result<EventType>.NotFound($"Event type '{eventTypeId}' was not found.");
        }

        return eventType;
    }
}

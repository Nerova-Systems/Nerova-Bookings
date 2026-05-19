using JetBrains.Annotations;
using Main.Features.BookingSideEffects.Domain;
using Main.Features.BookingSideEffects.Shared;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Features.BookingSideEffects.Queries;

[PublicAPI]
public sealed record GetEventTypeSideEffectDeliveriesQuery(EventTypeId EventTypeId) : IRequest<Result<BookingSideEffectDeliveriesResponse>>;

[PublicAPI]
public sealed record GetBookingSideEffectDeliveriesQuery(BookingId BookingId) : IRequest<Result<BookingSideEffectDeliveriesResponse>>;

public sealed class GetEventTypeSideEffectDeliveriesHandler(
    IBookingSideEffectDeliveryRepository deliveryRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetEventTypeSideEffectDeliveriesQuery, Result<BookingSideEffectDeliveriesResponse>>
{
    public async Task<Result<BookingSideEffectDeliveriesResponse>> Handle(GetEventTypeSideEffectDeliveriesQuery query, CancellationToken cancellationToken)
    {
        var authorization = CanViewSideEffects(executionContext);
        if (!authorization.IsSuccess) return Result<BookingSideEffectDeliveriesResponse>.From(authorization);

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, query.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<BookingSideEffectDeliveriesResponse>.From(eventType);

        var deliveries = await deliveryRepository.GetForEventTypeAsync(executionContext.TenantId!, query.EventTypeId, cancellationToken);
        return new BookingSideEffectDeliveriesResponse(deliveries.Select(BookingSideEffectDeliverySummaryResponse.From).ToArray());
    }

    private static Result CanViewSideEffects(IExecutionContext executionContext)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CalComWorkflows.Key) && !executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CalComWebhooks.Key))
        {
            return Result.Forbidden("Cal.com workflows and webhooks are disabled for this tenant.");
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
}

public sealed class GetBookingSideEffectDeliveriesHandler(
    IBookingSideEffectDeliveryRepository deliveryRepository,
    IBookingRepository bookingRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetBookingSideEffectDeliveriesQuery, Result<BookingSideEffectDeliveriesResponse>>
{
    public async Task<Result<BookingSideEffectDeliveriesResponse>> Handle(GetBookingSideEffectDeliveriesQuery query, CancellationToken cancellationToken)
    {
        var authorization = CanViewBookingSideEffects(executionContext);
        if (!authorization.IsSuccess) return Result<BookingSideEffectDeliveriesResponse>.From(authorization);

        var booking = await bookingRepository.GetForOwnerWithEventTypeAsync(executionContext.TenantId!, executionContext.UserInfo.Id!, query.BookingId, cancellationToken);
        if (booking is null)
        {
            return Result<BookingSideEffectDeliveriesResponse>.NotFound($"Booking '{query.BookingId}' was not found.");
        }

        var deliveries = await deliveryRepository.GetForBookingAsync(executionContext.TenantId!, query.BookingId, cancellationToken);
        return new BookingSideEffectDeliveriesResponse(deliveries.Select(BookingSideEffectDeliverySummaryResponse.From).ToArray());
    }

    private static Result CanViewBookingSideEffects(IExecutionContext executionContext)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CalComBookings.Key))
        {
            return Result.Forbidden("Cal.com bookings are disabled for this tenant.");
        }

        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CalComWorkflows.Key) && !executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CalComWebhooks.Key))
        {
            return Result.Forbidden("Cal.com workflows and webhooks are disabled for this tenant.");
        }

        if (executionContext.TenantId is null || executionContext.UserInfo.Id is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        return Result.Success();
    }
}

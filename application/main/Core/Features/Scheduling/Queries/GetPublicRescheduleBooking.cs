using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;

namespace Main.Features.Scheduling.Queries;

[PublicAPI]
public sealed record GetPublicRescheduleBookingQuery(BookingId Id, string Handle, string EventSlug) : IRequest<Result<PublicRescheduleBookingResponse>>
{
    public string Handle { get; } = Handle.Trim().ToLowerInvariant();

    public string EventSlug { get; } = EventSlug.Trim().ToLowerInvariant();
}

public sealed class GetPublicRescheduleBookingQueryValidator : AbstractValidator<GetPublicRescheduleBookingQuery>
{
    public GetPublicRescheduleBookingQueryValidator()
    {
        RuleFor(query => query.Handle).NotEmpty().MaximumLength(80);
        RuleFor(query => query.EventSlug).NotEmpty().MaximumLength(120);
    }
}

public sealed record PublicRescheduleBookingResponse(
    BookingId Id,
    string Handle,
    string EventSlug,
    string BookerName,
    string BookerEmail,
    string TimeZone,
    Dictionary<string, string> Responses,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status,
    bool CanReschedule,
    string? DisabledReason
);

public sealed class GetPublicRescheduleBookingHandler(
    PublicSchedulingResolver publicSchedulingResolver,
    IBookingRepository bookingRepository,
    TimeProvider timeProvider
) : IRequestHandler<GetPublicRescheduleBookingQuery, Result<PublicRescheduleBookingResponse>>
{
    public async Task<Result<PublicRescheduleBookingResponse>> Handle(GetPublicRescheduleBookingQuery query, CancellationToken cancellationToken)
    {
        var contextResult = await publicSchedulingResolver.ResolveAsync(query.Handle, query.EventSlug, null, cancellationToken);
        if (!contextResult.IsSuccess)
        {
            return Result<PublicRescheduleBookingResponse>.From(contextResult);
        }

        var context = contextResult.Value!;
        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(context.Profile.TenantId, context.Profile.OwnerUserId, context.EventType.TeamId, query.Id, cancellationToken);
        if (item is null || item.EventType.Id != context.EventType.Id)
        {
            return Result<PublicRescheduleBookingResponse>.NotFound($"Booking '{query.Id}' was not found.");
        }

        var action = BookingActionAvailability.ResolveReschedule(item.Booking, item.EventType, timeProvider.GetUtcNow());
        return new PublicRescheduleBookingResponse(
            item.Booking.Id,
            context.Profile.Handle,
            context.EventType.Slug,
            item.Booking.BookerName,
            item.Booking.BookerEmail,
            item.Booking.TimeZone,
            JsonSerializer.Deserialize<Dictionary<string, string>>(item.Booking.ResponsesJson) ?? [],
            item.Booking.StartTime,
            item.Booking.EndTime,
            item.Booking.Status,
            action.Enabled,
            action.DisabledReason
        );
    }
}

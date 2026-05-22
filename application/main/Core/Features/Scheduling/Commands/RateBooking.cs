using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

/// <summary>
///     Submit a 1–5 rating with optional feedback for a completed booking. Mirrors cal.com booking
///     ratings; gated to past, accepted bookings.
/// </summary>
[PublicAPI]
public sealed record RateBookingCommand(BookingId Id, int Rating, string? Feedback) : ICommand, IRequest<Result>;

public sealed class RateBookingValidator : AbstractValidator<RateBookingCommand>
{
    public RateBookingValidator()
    {
        RuleFor(command => command.Rating).InclusiveBetween(1, 5);
        RuleFor(command => command.Feedback).MaximumLength(2000);
    }
}

public sealed class RateBookingHandler(
    IBookingRepository bookingRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<RateBookingCommand, Result>
{
    public async Task<Result> Handle(RateBookingCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status != BookingStatus.Accepted)
        {
            return Result.BadRequest("Only accepted bookings can be rated.");
        }

        if (item.Booking.EndTime > timeProvider.GetUtcNow())
        {
            return Result.BadRequest("Bookings can only be rated after they end.");
        }

        item.Booking.Rate(command.Rating, command.Feedback);
        bookingRepository.Update(item.Booking);

        var payload = JsonSerializer.Serialize(new { rating = command.Rating });
        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.Rated,
            timeProvider.GetUtcNow(),
            ownerUserId,
            payload
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        return Result.Success();
    }
}

using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Queries;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Update)]
public sealed record RejectBookingCommand(BookingId Id, string? RejectionReason) : ICommand, IRequest<Result<BookingLifecycleResponse>>;

public sealed class RejectBookingValidator : AbstractValidator<RejectBookingCommand>
{
    public RejectBookingValidator()
    {
        RuleFor(command => command.RejectionReason).MaximumLength(1000);
    }
}

public sealed class RejectBookingHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<RejectBookingCommand, Result<BookingLifecycleResponse>>
{
    public async Task<Result<BookingLifecycleResponse>> Handle(RejectBookingCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingLifecycleResponse>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result<BookingLifecycleResponse>.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status != BookingStatus.Pending && item.Booking.Status != BookingStatus.AwaitingHost)
        {
            return Result<BookingLifecycleResponse>.BadRequest($"Booking '{command.Id}' is not awaiting confirmation.");
        }

        item.Booking.Reject(command.RejectionReason);
        bookingRepository.Update(item.Booking);

        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.Rejected,
            timeProvider.GetUtcNow(),
            ownerUserId
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        var attendees = await bookingAttendeeRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var attendeeResponses = attendees.Select(a => new BookingAttendeeResponse(a.Id, a.Name, a.Email, a.TimeZone, a.Locale, a.NoShow)).ToArray();
        return new BookingLifecycleResponse(item.Booking.Id, item.Booking.Status, attendeeResponses, item.Booking.LocationType, item.Booking.LocationValue);
    }
}

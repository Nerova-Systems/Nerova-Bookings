using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.ExecutionContext;
using Cqrs = SharedKernel.Cqrs;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
public sealed record ConfirmBookingCommand(BookingId Id) : Cqrs.ICommand, IRequest<Cqrs.Result<BookingLifecycleResponse>>;

[PublicAPI]
public sealed record RejectBookingCommand(BookingId Id, string? RejectionReason = null) : Cqrs.ICommand, IRequest<Cqrs.Result<BookingLifecycleResponse>>;

[PublicAPI]
public sealed record RequestRescheduleCommand(BookingId Id, string? RescheduleReason = null) : Cqrs.ICommand, IRequest<Cqrs.Result<BookingLifecycleResponse>>;

[PublicAPI]
public sealed record EditBookingLocationCommand(BookingId Id, string? LocationType, string? LocationValue) : Cqrs.ICommand, IRequest<Cqrs.Result<BookingLifecycleResponse>>;

[PublicAPI]
public sealed record AddBookingGuestsCommand(BookingId Id, BookingGuestRequest[] Guests) : Cqrs.ICommand, IRequest<Cqrs.Result<BookingLifecycleResponse>>;

[PublicAPI]
public sealed record BookingGuestRequest(string Name, string Email, string? TimeZone = null, string? PhoneNumber = null, string? Locale = null);

[PublicAPI]
public sealed record BookingLifecycleResponse(BookingId Id, string Status, BookingAttendee[] Attendees, string? LocationType, string? LocationValue);

public sealed class RejectBookingValidator : AbstractValidator<RejectBookingCommand>
{
    public RejectBookingValidator()
    {
        RuleFor(command => command.RejectionReason).MaximumLength(1000);
    }
}

public sealed class RequestRescheduleValidator : AbstractValidator<RequestRescheduleCommand>
{
    public RequestRescheduleValidator()
    {
        RuleFor(command => command.RescheduleReason).MaximumLength(1000);
    }
}

public sealed class EditBookingLocationValidator : AbstractValidator<EditBookingLocationCommand>
{
    public EditBookingLocationValidator()
    {
        RuleFor(command => command.LocationType).MaximumLength(80);
        RuleFor(command => command.LocationValue).MaximumLength(500);
    }
}

public sealed class AddBookingGuestsValidator : AbstractValidator<AddBookingGuestsCommand>
{
    public AddBookingGuestsValidator()
    {
        RuleFor(command => command.Guests).NotEmpty().WithMessage("At least one guest is required.");
        RuleForEach(command => command.Guests).ChildRules(guest =>
            {
                guest.RuleFor(g => g.Name).NotEmpty().MaximumLength(120);
                guest.RuleFor(g => g.Email).NotEmpty().EmailAddress().MaximumLength(320);
                guest.RuleFor(g => g.TimeZone).MaximumLength(100);
                guest.RuleFor(g => g.PhoneNumber).MaximumLength(80);
                guest.RuleFor(g => g.Locale).MaximumLength(20);
            }
        );
    }
}

public sealed class ConfirmBookingHandler(IBookingRepository bookingRepository, IExecutionContext executionContext)
    : IRequestHandler<ConfirmBookingCommand, Cqrs.Result<BookingLifecycleResponse>>
{
    public async Task<Cqrs.Result<BookingLifecycleResponse>> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var itemResult = await BookingLifecycleCommandGuard.GetBookingAsync(bookingRepository, executionContext, command.Id, cancellationToken);
        if (!itemResult.IsSuccess) return Cqrs.Result<BookingLifecycleResponse>.From(itemResult);

        var booking = itemResult.Value!.Booking;
        if (booking.Status is "cancelled" or "rejected")
        {
            return Cqrs.Result<BookingLifecycleResponse>.BadRequest("Cancelled or rejected bookings cannot be confirmed.");
        }

        booking.Confirm();
        bookingRepository.Update(booking);
        return ToResponse(booking);
    }

    private static BookingLifecycleResponse ToResponse(Booking booking)
    {
        return new BookingLifecycleResponse(booking.Id, booking.Status, booking.Attendees, booking.LocationType, booking.LocationValue);
    }
}

public sealed class RejectBookingHandler(IBookingRepository bookingRepository, IExecutionContext executionContext)
    : IRequestHandler<RejectBookingCommand, Cqrs.Result<BookingLifecycleResponse>>
{
    public async Task<Cqrs.Result<BookingLifecycleResponse>> Handle(RejectBookingCommand command, CancellationToken cancellationToken)
    {
        var itemResult = await BookingLifecycleCommandGuard.GetBookingAsync(bookingRepository, executionContext, command.Id, cancellationToken);
        if (!itemResult.IsSuccess) return Cqrs.Result<BookingLifecycleResponse>.From(itemResult);

        var booking = itemResult.Value!.Booking;
        if (booking.Status == "cancelled")
        {
            return Cqrs.Result<BookingLifecycleResponse>.BadRequest("Cancelled bookings cannot be rejected.");
        }

        booking.Reject(command.RejectionReason);
        bookingRepository.Update(booking);
        return new BookingLifecycleResponse(booking.Id, booking.Status, booking.Attendees, booking.LocationType, booking.LocationValue);
    }
}

public sealed class RequestRescheduleHandler(IBookingRepository bookingRepository, IExecutionContext executionContext)
    : IRequestHandler<RequestRescheduleCommand, Cqrs.Result<BookingLifecycleResponse>>
{
    public async Task<Cqrs.Result<BookingLifecycleResponse>> Handle(RequestRescheduleCommand command, CancellationToken cancellationToken)
    {
        var itemResult = await BookingLifecycleCommandGuard.GetBookingAsync(bookingRepository, executionContext, command.Id, cancellationToken);
        if (!itemResult.IsSuccess) return Cqrs.Result<BookingLifecycleResponse>.From(itemResult);

        var booking = itemResult.Value!.Booking;
        if (booking.Status is "cancelled" or "rejected")
        {
            return Cqrs.Result<BookingLifecycleResponse>.BadRequest("Cannot request reschedule for cancelled or rejected booking.");
        }

        booking.RequestReschedule(command.RescheduleReason, executionContext.UserInfo.Email);
        bookingRepository.Update(booking);
        return new BookingLifecycleResponse(booking.Id, booking.Status, booking.Attendees, booking.LocationType, booking.LocationValue);
    }
}

public sealed class EditBookingLocationHandler(IBookingRepository bookingRepository, IExecutionContext executionContext)
    : IRequestHandler<EditBookingLocationCommand, Cqrs.Result<BookingLifecycleResponse>>
{
    public async Task<Cqrs.Result<BookingLifecycleResponse>> Handle(EditBookingLocationCommand command, CancellationToken cancellationToken)
    {
        var itemResult = await BookingLifecycleCommandGuard.GetBookingAsync(bookingRepository, executionContext, command.Id, cancellationToken);
        if (!itemResult.IsSuccess) return Cqrs.Result<BookingLifecycleResponse>.From(itemResult);

        var booking = itemResult.Value!.Booking;
        if (booking.Status is "cancelled" or "rejected")
        {
            return Cqrs.Result<BookingLifecycleResponse>.BadRequest("Cancelled or rejected bookings cannot change location.");
        }

        booking.EditLocation(command.LocationType, command.LocationValue);
        bookingRepository.Update(booking);
        return new BookingLifecycleResponse(booking.Id, booking.Status, booking.Attendees, booking.LocationType, booking.LocationValue);
    }
}

public sealed class AddBookingGuestsHandler(IBookingRepository bookingRepository, IExecutionContext executionContext)
    : IRequestHandler<AddBookingGuestsCommand, Cqrs.Result<BookingLifecycleResponse>>
{
    public async Task<Cqrs.Result<BookingLifecycleResponse>> Handle(AddBookingGuestsCommand command, CancellationToken cancellationToken)
    {
        var itemResult = await BookingLifecycleCommandGuard.GetBookingAsync(bookingRepository, executionContext, command.Id, cancellationToken);
        if (!itemResult.IsSuccess) return Cqrs.Result<BookingLifecycleResponse>.From(itemResult);

        var booking = itemResult.Value!.Booking;
        if (booking.Status is "cancelled" or "rejected")
        {
            return Cqrs.Result<BookingLifecycleResponse>.BadRequest("Cancelled or rejected bookings cannot add guests.");
        }

        booking.AddGuests(command.Guests.Select(guest => new BookingAttendee(guest.Name, guest.Email, guest.TimeZone ?? booking.TimeZone, guest.PhoneNumber, guest.Locale, false)).ToArray());
        bookingRepository.Update(booking);
        return new BookingLifecycleResponse(booking.Id, booking.Status, booking.Attendees, booking.LocationType, booking.LocationValue);
    }
}

internal static class BookingLifecycleCommandGuard
{
    public static async Task<Cqrs.Result<BookingWithEventType>> GetBookingAsync(IBookingRepository bookingRepository, IExecutionContext executionContext, BookingId bookingId, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagRegistry.CalComBookings.Key))
        {
            return Cqrs.Result<BookingWithEventType>.Forbidden("Cal.com bookings are disabled for this tenant.");
        }

        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Cqrs.Result<BookingWithEventType>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, bookingId, cancellationToken);
        if (item is null)
        {
            return Cqrs.Result<BookingWithEventType>.NotFound($"Booking '{bookingId}' was not found.");
        }

        return item;
    }
}

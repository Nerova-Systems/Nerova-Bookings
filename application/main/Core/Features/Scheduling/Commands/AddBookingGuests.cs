using System.Text.Json;
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
public sealed record AddBookingGuestsCommand(BookingId Id, BookingGuestInput[] Guests) : ICommand, IRequest<Result<BookingLifecycleResponse>>;

[PublicAPI]
public sealed record BookingGuestInput(string Name, string Email, string TimeZone, string? Locale = null);

public sealed class AddBookingGuestsValidator : AbstractValidator<AddBookingGuestsCommand>
{
    public AddBookingGuestsValidator()
    {
        RuleFor(command => command.Guests).NotEmpty();
        RuleForEach(command => command.Guests).ChildRules(guest =>
            {
                guest.RuleFor(g => g.Name).NotEmpty().MaximumLength(120);
                guest.RuleFor(g => g.Email).NotEmpty().EmailAddress().MaximumLength(320);
                guest.RuleFor(g => g.TimeZone).NotEmpty().MaximumLength(100);
                guest.RuleFor(g => g.Locale).MaximumLength(20);
            }
        );
    }
}

public sealed class AddBookingGuestsHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<AddBookingGuestsCommand, Result<BookingLifecycleResponse>>
{
    public async Task<Result<BookingLifecycleResponse>> Handle(AddBookingGuestsCommand command, CancellationToken cancellationToken)
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

        if (item.Booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Result<BookingLifecycleResponse>.BadRequest("Closed bookings cannot have guests added.");
        }

        var existing = await bookingAttendeeRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var existingEmails = existing.Select(attendee => attendee.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = new List<string>();
        var newAttendees = new List<BookingAttendee>();
        foreach (var guest in command.Guests)
        {
            var normalizedEmail = guest.Email.Trim().ToLowerInvariant();
            if (existingEmails.Contains(normalizedEmail))
            {
                continue;
            }

            var attendee = BookingAttendee.Create(tenantId, item.Booking.Id, guest.Name, normalizedEmail, guest.TimeZone, guest.Locale ?? string.Empty);
            await bookingAttendeeRepository.AddAsync(attendee, cancellationToken);
            existingEmails.Add(normalizedEmail);
            added.Add(normalizedEmail);
            newAttendees.Add(attendee);
        }

        if (added.Count > 0)
        {
            var payload = JsonSerializer.Serialize(new { addedEmails = added });
            var entry = BookingHistoryEntry.Create(
                tenantId,
                item.Booking.Id,
                BookingHistoryEventType.GuestAdded,
                timeProvider.GetUtcNow(),
                ownerUserId,
                payload
            );
            await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);
        }

        var allAttendees = existing.Concat(newAttendees).ToArray();
        var attendeeResponses = allAttendees.Select(a => new BookingAttendeeResponse(a.Id, a.Name, a.Email, a.TimeZone, a.Locale, a.NoShow)).ToArray();
        return new BookingLifecycleResponse(item.Booking.Id, item.Booking.Status, attendeeResponses, item.Booking.LocationType, item.Booking.LocationValue);
    }
}

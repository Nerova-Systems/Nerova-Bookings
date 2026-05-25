using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Update)]
public sealed record AddBookingGuestsCommand(BookingId Id, BookingGuestInput[] Guests) : ICommand, IRequest<Result>;

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
        });
    }
}

public sealed class AddBookingGuestsHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<AddBookingGuestsCommand, Result>
{
    public async Task<Result> Handle(AddBookingGuestsCommand command, CancellationToken cancellationToken)
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

        if (item.Booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Result.BadRequest("Closed bookings cannot have guests added.");
        }

        var existing = await bookingAttendeeRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var existingEmails = existing.Select(attendee => attendee.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = new List<string>();
        foreach (var guest in command.Guests)
        {
            var normalizedEmail = guest.Email.Trim().ToLowerInvariant();
            if (existingEmails.Contains(normalizedEmail))
            {
                continue;
            }

            var attendee = BookingAttendee.Create(tenantId, item.Booking.Id, guest.Name, guest.Email, guest.TimeZone, guest.Locale ?? string.Empty);
            await bookingAttendeeRepository.AddAsync(attendee, cancellationToken);
            existingEmails.Add(normalizedEmail);
            added.Add(normalizedEmail);
        }

        if (added.Count == 0)
        {
            return Result.Success();
        }

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

        return Result.Success();
    }
}

using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
public sealed record AddBookingInternalNoteCommand(BookingId Id, string Body) : ICommand, IRequest<Result<BookingInternalNoteId>>;

public sealed class AddBookingInternalNoteValidator : AbstractValidator<AddBookingInternalNoteCommand>
{
    public AddBookingInternalNoteValidator()
    {
        RuleFor(command => command.Body).NotEmpty().MaximumLength(5000);
    }
}

public sealed class AddBookingInternalNoteHandler(
    IBookingRepository bookingRepository,
    IBookingInternalNoteRepository bookingInternalNoteRepository,
    IExecutionContext executionContext
) : IRequestHandler<AddBookingInternalNoteCommand, Result<BookingInternalNoteId>>
{
    public async Task<Result<BookingInternalNoteId>> Handle(AddBookingInternalNoteCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingInternalNoteId>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result<BookingInternalNoteId>.NotFound($"Booking '{command.Id}' was not found.");
        }

        var note = BookingInternalNote.Create(tenantId, item.Booking.Id, ownerUserId, command.Body);
        await bookingInternalNoteRepository.AddAsync(note, cancellationToken);

        return note.Id;
    }
}

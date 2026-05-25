using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Update)]
public sealed record DeleteBookingInternalNoteCommand(BookingId Id, BookingInternalNoteId NoteId) : ICommand, IRequest<Result>;

public sealed class DeleteBookingInternalNoteHandler(
    IBookingRepository bookingRepository,
    IBookingInternalNoteRepository bookingInternalNoteRepository,
    IExecutionContext executionContext
) : IRequestHandler<DeleteBookingInternalNoteCommand, Result>
{
    public async Task<Result> Handle(DeleteBookingInternalNoteCommand command, CancellationToken cancellationToken)
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

        var note = await bookingInternalNoteRepository.GetByIdForBookingAsync(item.Booking.Id, command.NoteId, cancellationToken);
        if (note is null)
        {
            return Result.NotFound($"Note '{command.NoteId}' was not found.");
        }

        bookingInternalNoteRepository.Remove(note);

        return Result.Success();
    }
}

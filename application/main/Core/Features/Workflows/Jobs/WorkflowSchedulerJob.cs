using Main.Features.Workflows.Domain;
using Main.Features.Workflows.EventHandlers;
using Main.Features.Workflows.Infrastructure;
using SharedKernel.Persistence;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace Main.Features.Workflows.Jobs;

/// <summary>
///     Polls for booking lifecycle changes (new, cancelled, rescheduled) and creates/cancels
///     workflow reminders accordingly. Runs every 60 seconds.
/// </summary>
public sealed class WorkflowSchedulerJob(
    WorkflowBookingReader bookingReader,
    BookingCreatedWorkflowHandler createdHandler,
    BookingCancelledWorkflowHandler cancelledHandler,
    BookingRescheduledWorkflowHandler rescheduledHandler,
    IUnitOfWork unitOfWork
) : ITickerFunction
{
    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        // Handle new bookings
        var newBookings = await bookingReader.GetNewBookingsAsync(ct);
        foreach (var item in newBookings)
        {
            foreach (var workflowWithBinding in item.WorkflowBindings)
            {
                await createdHandler.HandleAsync(item.Booking, workflowWithBinding.Workflow, ct);
            }
        }

        // Handle cancelled bookings
        var cancelledBookings = await bookingReader.GetCancelledBookingsWithPendingRemindersAsync(ct);
        foreach (var booking in cancelledBookings)
        {
            await cancelledHandler.HandleAsync(booking, ct);
        }

        // Handle rescheduled bookings
        var rescheduledBookings = await bookingReader.GetRescheduledBookingsAsync(ct);
        foreach (var item in rescheduledBookings)
        {
            await rescheduledHandler.HandleAsync(item.Booking, ct);
        }

        if (newBookings.Length > 0 || cancelledBookings.Length > 0 || rescheduledBookings.Length > 0)
        {
            await unitOfWork.CommitAsync(ct);
        }
    }
}

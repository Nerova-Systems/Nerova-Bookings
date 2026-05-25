using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Notifications;
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
///     <para>
///         <b>Email notifications.</b> Alongside the workflow-reminder handlers, this job dispatches
///         confirmation/cancellation/reschedule emails via <see cref="IBookingNotificationDispatcher" />.
///         Today this covers bookings whose event type has at least one active workflow (because
///         the reader gates on workflow bindings/pending reminders). Coverage for workflow-less
///         bookings is deferred — it requires either domain events or a per-booking notification
///         ledger; both are out of scope for the initial email-dispatcher slice.
///     </para>
/// </summary>
public sealed class WorkflowSchedulerJob(
    WorkflowBookingReader bookingReader,
    BookingCreatedWorkflowHandler createdHandler,
    BookingCancelledWorkflowHandler cancelledHandler,
    BookingRescheduledWorkflowHandler rescheduledHandler,
    IBookingNotificationDispatcher notificationDispatcher,
    IEventTypeRepository eventTypeRepository,
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

            // Dispatch confirmation email once per booking (independent of how many workflows
            // are bound to it). Failures are surfaced via the dispatcher's own logging; we do
            // not swallow them here — the job is idempotent on the reminder side because the
            // reader gates on `HasReminders`, so a retry won't double-create reminders.
            var eventType = await eventTypeRepository.GetByIdAsync(item.Booking.EventTypeId, ct);
            await notificationDispatcher.DispatchAsync(item.Booking, eventType, BookingNotificationKind.Created, ct);
        }

        // Handle cancelled bookings
        var cancelledBookings = await bookingReader.GetCancelledBookingsWithPendingRemindersAsync(ct);
        foreach (var booking in cancelledBookings)
        {
            await cancelledHandler.HandleAsync(booking, ct);

            var eventType = await eventTypeRepository.GetByIdAsync(booking.EventTypeId, ct);
            await notificationDispatcher.DispatchAsync(booking, eventType, BookingNotificationKind.Cancelled, ct);
        }

        // Handle rescheduled bookings
        var rescheduledBookings = await bookingReader.GetRescheduledBookingsAsync(ct);
        foreach (var item in rescheduledBookings)
        {
            await rescheduledHandler.HandleAsync(item.Booking, ct);

            var eventType = await eventTypeRepository.GetByIdAsync(item.Booking.EventTypeId, ct);
            await notificationDispatcher.DispatchAsync(item.Booking, eventType, BookingNotificationKind.Rescheduled, ct);
        }

        if (newBookings.Length > 0 || cancelledBookings.Length > 0 || rescheduledBookings.Length > 0)
        {
            await unitOfWork.CommitAsync(ct);
        }
    }
}

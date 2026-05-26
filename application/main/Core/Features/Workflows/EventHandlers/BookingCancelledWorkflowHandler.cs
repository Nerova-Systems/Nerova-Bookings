using Main.Features.Scheduling.Domain;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Services;
using Main.Features.Workflows.Infrastructure;

namespace Main.Features.Workflows.EventHandlers;

/// <summary>
///     Cancels pending reminders and optionally creates an immediate EVENT_CANCELLED reminder
///     when a booking cancellation is detected by the scheduler job.
/// </summary>
public sealed class BookingCancelledWorkflowHandler(
    IWorkflowReminderRepository reminderRepository,
    IWorkflowRepository workflowRepository,
    WorkflowReminderScheduler scheduler,
    TimeProvider timeProvider
)
{
    public async Task HandleAsync(Booking booking, CancellationToken ct)
    {
        var pendingReminders = await reminderRepository.GetPendingForBookingAsync(booking.Id, ct);
        foreach (var reminder in pendingReminders)
        {
            reminder.MarkCancelled();
        }

        if (pendingReminders.Length > 0)
        {
            reminderRepository.UpdateRange(pendingReminders);
        }

        // Find workflows bound to this event type that have an EventCancelled trigger
        var workflowIds = pendingReminders.Select(r => r.WorkflowId).Distinct().ToArray();
        var now = timeProvider.GetUtcNow();

        foreach (var workflowId in workflowIds)
        {
            var workflow = await workflowRepository.GetByIdAsync(workflowId, ct);
            if (workflow is null || workflow.Trigger != WorkflowTrigger.EventCancelled) continue;

            foreach (var step in workflow.Steps)
            {
                var scheduledDate = scheduler.CalculateScheduledDate(
                    workflow.Trigger,
                    step.ReminderTime,
                    step.TimeUnit,
                    booking.StartTime,
                    booking.EndTime,
                    now
                );

                if (scheduledDate is null) continue;

                var reminder = WorkflowReminder.Create(
                    booking.TenantId,
                    workflow.Id,
                    step.Id,
                    booking.Id,
                    booking.StartTime,
                    scheduledDate.Value,
                    step.Action,
                    step.Template,
                    step.SendTo,
                    step.EmailSubject,
                    step.EmailBody
                );

                await reminderRepository.AddAsync(reminder, ct);
            }
        }
    }
}

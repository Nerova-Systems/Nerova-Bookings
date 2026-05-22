using Main.Features.Scheduling.Domain;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Services;

namespace Main.Features.Workflows.EventHandlers;

/// <summary>
///     Cancels stale reminders and creates fresh ones when a booking reschedule is detected.
/// </summary>
public sealed class BookingRescheduledWorkflowHandler(
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

        // Re-create reminders for workflows that support rescheduling
        var workflowIds = pendingReminders.Select(r => r.WorkflowId).Distinct().ToArray();
        var now = timeProvider.GetUtcNow();

        foreach (var workflowId in workflowIds)
        {
            var workflow = await workflowRepository.GetByIdAsync(workflowId, ct);
            if (workflow is null) continue;

            // Only reschedule applicable trigger types
            if (workflow.Trigger is not (WorkflowTrigger.BeforeEvent or WorkflowTrigger.AfterEvent or WorkflowTrigger.RescheduleEvent)) continue;

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

                // Skip past-due before-event reminders
                if (workflow.Trigger == WorkflowTrigger.BeforeEvent && scheduledDate.Value <= now) continue;

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

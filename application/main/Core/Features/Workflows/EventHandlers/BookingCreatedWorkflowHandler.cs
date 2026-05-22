using Main.Features.Scheduling.Domain;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Services;
using SharedKernel.Persistence;

namespace Main.Features.Workflows.EventHandlers;

/// <summary>
///     Creates workflow reminders when a new booking is detected by the scheduler job.
/// </summary>
public sealed class BookingCreatedWorkflowHandler(
    IWorkflowReminderRepository reminderRepository,
    WorkflowReminderScheduler scheduler,
    TimeProvider timeProvider
)
{
    public async Task HandleAsync(Booking booking, Workflow workflow, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var step in workflow.Steps)
        {
            if (!IsApplicableTrigger(workflow.Trigger)) continue;

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

    private static bool IsApplicableTrigger(WorkflowTrigger trigger)
    {
        return trigger is WorkflowTrigger.NewEvent
            or WorkflowTrigger.BeforeEvent
            or WorkflowTrigger.AfterEvent;
    }
}

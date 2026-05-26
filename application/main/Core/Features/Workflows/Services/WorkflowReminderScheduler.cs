using Main.Features.Workflows.Domain;

namespace Main.Features.Workflows.Services;

public sealed class WorkflowReminderScheduler
{
    private static readonly Dictionary<WorkflowTimeUnit, int> MinutesPerUnit = new()
    {
        { WorkflowTimeUnit.Minutes, 1 },
        { WorkflowTimeUnit.Hours, 60 },
        { WorkflowTimeUnit.Days, 1440 },
        { WorkflowTimeUnit.Weeks, 10080 }
    };

    /// <summary>
    ///     Calculates the scheduled dispatch date for a workflow reminder.
    ///     Returns null when the trigger does not support time-based scheduling (e.g. NewEvent, EventCancelled,
    ///     RescheduleEvent).
    /// </summary>
    public DateTimeOffset? CalculateScheduledDate(
        WorkflowTrigger trigger,
        int? reminderTime,
        WorkflowTimeUnit? timeUnit,
        DateTimeOffset bookingStartTime,
        DateTimeOffset bookingEndTime,
        DateTimeOffset now
    )
    {
        return trigger switch
        {
            WorkflowTrigger.BeforeEvent when reminderTime.HasValue && timeUnit.HasValue
                => bookingStartTime.AddMinutes(-reminderTime.Value * MinutesPerUnit[timeUnit.Value]),

            WorkflowTrigger.AfterEvent when reminderTime.HasValue && timeUnit.HasValue
                => bookingEndTime.AddMinutes(reminderTime.Value * MinutesPerUnit[timeUnit.Value]),

            WorkflowTrigger.NewEvent => now,
            WorkflowTrigger.EventCancelled => now,
            WorkflowTrigger.RescheduleEvent => now,

            _ => null
        };
    }
}

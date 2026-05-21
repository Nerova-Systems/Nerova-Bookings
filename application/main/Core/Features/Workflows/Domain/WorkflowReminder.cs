using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;

namespace Main.Features.Workflows.Domain;

public sealed class WorkflowReminder : AggregateRoot<WorkflowReminderId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private WorkflowReminder() : base(WorkflowReminderId.NewId())
    {
        WorkflowId = new WorkflowId(string.Empty);
        BookingId = new BookingId(string.Empty);
    }

    private WorkflowReminder(
        TenantId tenantId,
        WorkflowId workflowId,
        WorkflowStepId stepId,
        BookingId bookingId,
        DateTimeOffset bookingStartTime,
        DateTimeOffset scheduledDate,
        WorkflowAction action,
        WorkflowReminderTemplate template,
        string? sendTo,
        string? emailSubject,
        string? emailBody
    ) : base(WorkflowReminderId.NewId())
    {
        TenantId = tenantId;
        WorkflowId = workflowId;
        StepId = stepId;
        BookingId = bookingId;
        BookingStartTime = bookingStartTime;
        ScheduledDate = scheduledDate;
        Status = WorkflowReminderStatus.Pending;
        Action = action;
        Template = template;
        SendTo = sendTo;
        EmailSubject = emailSubject;
        EmailBody = emailBody;
    }

    public WorkflowId WorkflowId { get; private set; }

    public WorkflowStepId? StepId { get; private set; }

    public BookingId BookingId { get; private set; }

    /// <summary>
    ///     Snapshot of booking start time at creation. Used to detect rescheduled bookings.
    /// </summary>
    public DateTimeOffset BookingStartTime { get; private set; }

    public DateTimeOffset ScheduledDate { get; private set; }

    public WorkflowReminderStatus Status { get; private set; }

    public WorkflowAction Action { get; private set; }

    public WorkflowReminderTemplate Template { get; private set; }

    /// <summary>
    ///     Specific email or phone number to send to. Null when sending to host/attendee (resolved at dispatch time).
    /// </summary>
    public string? SendTo { get; private set; }

    public string? EmailSubject { get; private set; }

    public string? EmailBody { get; private set; }

    /// <summary>
    ///     External provider reference (e.g. SMS/WhatsApp message ID).
    /// </summary>
    public string? ReferenceId { get; private set; }

    public string? ErrorMessage { get; private set; }

    public int RetryCount { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static WorkflowReminder Create(
        TenantId tenantId,
        WorkflowId workflowId,
        WorkflowStepId stepId,
        BookingId bookingId,
        DateTimeOffset bookingStartTime,
        DateTimeOffset scheduledDate,
        WorkflowAction action,
        WorkflowReminderTemplate template,
        string? sendTo,
        string? emailSubject,
        string? emailBody
    )
    {
        return new WorkflowReminder(tenantId, workflowId, stepId, bookingId, bookingStartTime, scheduledDate, action, template, sendTo, emailSubject, emailBody);
    }

    public void MarkDispatched(string? referenceId = null)
    {
        Status = WorkflowReminderStatus.Dispatched;
        ReferenceId = referenceId;
    }

    public void MarkCancelled()
    {
        Status = WorkflowReminderStatus.Cancelled;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = WorkflowReminderStatus.Failed;
        ErrorMessage = errorMessage;
        RetryCount++;
    }
}

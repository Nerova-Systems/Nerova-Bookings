using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Workflows.Domain;

[IdPrefix("wfstep")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WorkflowStepId>))]
public sealed record WorkflowStepId(string Value) : StronglyTypedUlid<WorkflowStepId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

[IdPrefix("wf")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WorkflowId>))]
public sealed record WorkflowId(string Value) : StronglyTypedUlid<WorkflowId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

[IdPrefix("wfbind")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WorkflowEventTypeBindingId>))]
public sealed record WorkflowEventTypeBindingId(string Value) : StronglyTypedUlid<WorkflowEventTypeBindingId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

[IdPrefix("wfrem")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WorkflowReminderId>))]
public sealed record WorkflowReminderId(string Value) : StronglyTypedUlid<WorkflowReminderId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class WorkflowStep : Entity<WorkflowStepId>
{
    [UsedImplicitly]
    private WorkflowStep() : base(WorkflowStepId.NewId())
    {
    }

    private WorkflowStep(
        WorkflowAction action,
        WorkflowReminderTemplate template,
        int? reminderTime,
        WorkflowTimeUnit? timeUnit,
        string? sendTo,
        string? emailSubject,
        string? emailBody
    ) : base(WorkflowStepId.NewId())
    {
        Action = action;
        Template = template;
        ReminderTime = reminderTime;
        TimeUnit = timeUnit;
        SendTo = sendTo;
        EmailSubject = emailSubject;
        EmailBody = emailBody;
    }

    public WorkflowAction Action { get; private set; }

    public WorkflowReminderTemplate Template { get; private set; }

    /// <summary>
    ///     Number of time units before/after the event. Only relevant for BeforeEvent and AfterEvent triggers.
    /// </summary>
    public int? ReminderTime { get; private set; }

    /// <summary>
    ///     Time unit for <see cref="ReminderTime" />. Only relevant for BeforeEvent and AfterEvent triggers.
    /// </summary>
    public WorkflowTimeUnit? TimeUnit { get; private set; }

    /// <summary>
    ///     Specific email address or phone number to send to. Null when sending to host/attendee.
    /// </summary>
    public string? SendTo { get; private set; }

    public string? EmailSubject { get; private set; }

    public string? EmailBody { get; private set; }

    internal static WorkflowStep Create(
        WorkflowAction action,
        WorkflowReminderTemplate template,
        int? reminderTime,
        WorkflowTimeUnit? timeUnit,
        string? sendTo,
        string? emailSubject,
        string? emailBody
    )
    {
        return new WorkflowStep(action, template, reminderTime, timeUnit, sendTo, emailSubject, emailBody);
    }

    internal void Update(
        WorkflowAction action,
        WorkflowReminderTemplate template,
        int? reminderTime,
        WorkflowTimeUnit? timeUnit,
        string? sendTo,
        string? emailSubject,
        string? emailBody
    )
    {
        Action = action;
        Template = template;
        ReminderTime = reminderTime;
        TimeUnit = timeUnit;
        SendTo = string.IsNullOrWhiteSpace(sendTo) ? null : sendTo.Trim();
        EmailSubject = string.IsNullOrWhiteSpace(emailSubject) ? null : emailSubject.Trim();
        EmailBody = string.IsNullOrWhiteSpace(emailBody) ? null : emailBody.Trim();
    }
}

public sealed class Workflow : SoftDeletableAggregateRoot<WorkflowId>, ITenantScopedEntity
{
    private readonly List<WorkflowStep> _steps = [];

    [UsedImplicitly]
    private Workflow() : base(WorkflowId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        Name = string.Empty;
    }

    private Workflow(TenantId tenantId, UserId ownerUserId, string name, WorkflowTrigger trigger)
        : base(WorkflowId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Name = name.Trim();
        Trigger = trigger;
    }

    public UserId OwnerUserId { get; private set; }

    public string Name { get; private set; }

    public WorkflowTrigger Trigger { get; private set; }

    public IReadOnlyList<WorkflowStep> Steps => _steps.AsReadOnly();

    public TenantId TenantId { get; } = new(0);

    public static Workflow Create(TenantId tenantId, UserId ownerUserId, string name, WorkflowTrigger trigger)
    {
        return new Workflow(tenantId, ownerUserId, name, trigger);
    }

    public void Update(string name)
    {
        Name = name.Trim();
    }

    public WorkflowStep AddStep(
        WorkflowAction action,
        WorkflowReminderTemplate template,
        int? reminderTime,
        WorkflowTimeUnit? timeUnit,
        string? sendTo,
        string? emailSubject,
        string? emailBody
    )
    {
        var step = WorkflowStep.Create(action, template, reminderTime, timeUnit, sendTo, emailSubject, emailBody);
        _steps.Add(step);
        return step;
    }

    public bool UpdateStep(
        WorkflowStepId stepId,
        WorkflowAction action,
        WorkflowReminderTemplate template,
        int? reminderTime,
        WorkflowTimeUnit? timeUnit,
        string? sendTo,
        string? emailSubject,
        string? emailBody
    )
    {
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return false;
        step.Update(action, template, reminderTime, timeUnit, sendTo, emailSubject, emailBody);
        return true;
    }

    public bool RemoveStep(WorkflowStepId stepId)
    {
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return false;
        _steps.Remove(step);
        return true;
    }
}

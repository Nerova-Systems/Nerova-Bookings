using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.BookingSideEffects.Domain;

[IdPrefix("flow")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WorkflowId>))]
public sealed record WorkflowId(string Value) : StronglyTypedUlid<WorkflowId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class Workflow : AggregateRoot<WorkflowId>, ITenantScopedEntity
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    [UsedImplicitly]
    private Workflow() : base(WorkflowId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        EventTypeId = new EventTypeId(string.Empty);
        Name = string.Empty;
        Trigger = string.Empty;
        StepsJson = "[]";
    }

    private Workflow(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        string name,
        bool active,
        string trigger,
        int? scheduledOffsetMinutes,
        WorkflowStep[] steps
    ) : base(WorkflowId.NewId())
    {
        Name = string.Empty;
        Trigger = string.Empty;
        StepsJson = "[]";
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        EventTypeId = eventTypeId;
        Update(name, active, trigger, scheduledOffsetMinutes, steps);
    }

    public UserId OwnerUserId { get; private set; }

    public EventTypeId EventTypeId { get; private set; }

    public string Name { get; private set; }

    public bool Active { get; private set; }

    public string Trigger { get; private set; }

    public int? ScheduledOffsetMinutes { get; private set; }

    public string StepsJson { get; private set; }

    [NotMapped]
    public WorkflowStep[] Steps => JsonSerializer.Deserialize<WorkflowStep[]>(StepsJson, JsonSerializerOptions) ?? [];

    public TenantId TenantId { get; } = new(0);

    public static Workflow Create(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        string name,
        bool active,
        string trigger,
        int? scheduledOffsetMinutes,
        WorkflowStep[] steps
    )
    {
        return new Workflow(tenantId, ownerUserId, eventTypeId, name, active, trigger, scheduledOffsetMinutes, steps);
    }

    public void Update(string name, bool active, string trigger, int? scheduledOffsetMinutes, WorkflowStep[] steps)
    {
        Name = name.Trim();
        Active = active;
        Trigger = trigger.Trim().ToUpperInvariant();
        ScheduledOffsetMinutes = scheduledOffsetMinutes;
        StepsJson = JsonSerializer.Serialize(steps.Select(NormalizeStep).ToArray(), JsonSerializerOptions);
    }

    public void Disable()
    {
        Active = false;
    }

    private static WorkflowStep NormalizeStep(WorkflowStep step)
    {
        return new WorkflowStep(
            step.Kind.Trim().ToLowerInvariant(),
            step.Recipient.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(step.Subject) ? null : step.Subject.Trim(),
            string.IsNullOrWhiteSpace(step.Body) ? null : step.Body.Trim(),
            step.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
        );
    }
}

public sealed record WorkflowStep(
    string Kind,
    string Recipient,
    string? Subject,
    string? Body,
    Dictionary<string, string>? Metadata = null
);

namespace Main.Features.Workflows.Domain;

public enum WorkflowTrigger
{
    BeforeEvent,
    NewEvent,
    EventCancelled,
    AfterEvent,
    RescheduleEvent
}

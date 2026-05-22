using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Workflows.Domain;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features;

// This file contains all the telemetry events that are collected by the application. Telemetry events are important
// to understand how the application is being used and collect valuable information for the business. Quality is
// important, and keeping all the telemetry events in one place makes it easier to maintain high quality.
// This particular includes the naming of the telemetry events (which should be in past tense) and the properties that
// are collected with each telemetry event. Since missing or bad data cannot be fixed, it is important to have a good
// data quality from the start.
public sealed class ScheduleCreated(ScheduleId scheduleId)
    : TelemetryEvent(("schedule_id", scheduleId));

public sealed class ScheduleUpdated(ScheduleId scheduleId)
    : TelemetryEvent(("schedule_id", scheduleId));

public sealed class ScheduleDeleted(ScheduleId scheduleId)
    : TelemetryEvent(("schedule_id", scheduleId));

public sealed class ScheduleDuplicated(ScheduleId sourceScheduleId, ScheduleId duplicateScheduleId)
    : TelemetryEvent(("source_schedule_id", sourceScheduleId), ("schedule_id", duplicateScheduleId));

public sealed class EventTypeCreated(EventTypeId eventTypeId)
    : TelemetryEvent(("event_type_id", eventTypeId));

public sealed class EventTypeUpdated(EventTypeId eventTypeId)
    : TelemetryEvent(("event_type_id", eventTypeId));

public sealed class EventTypeDeleted(EventTypeId eventTypeId)
    : TelemetryEvent(("event_type_id", eventTypeId));

public sealed class WorkflowCreated(WorkflowId workflowId)
    : TelemetryEvent(("workflow_id", workflowId));

public sealed class WorkflowUpdated(WorkflowId workflowId)
    : TelemetryEvent(("workflow_id", workflowId));

public sealed class WorkflowDeleted(WorkflowId workflowId)
    : TelemetryEvent(("workflow_id", workflowId));

public sealed class WorkflowStepAdded(WorkflowId workflowId, WorkflowStepId stepId)
    : TelemetryEvent(("workflow_id", workflowId), ("step_id", stepId));

public sealed class WorkflowStepUpdated(WorkflowId workflowId, WorkflowStepId stepId)
    : TelemetryEvent(("workflow_id", workflowId), ("step_id", stepId));

public sealed class WorkflowStepDeleted(WorkflowId workflowId, WorkflowStepId stepId)
    : TelemetryEvent(("workflow_id", workflowId), ("step_id", stepId));

public sealed class WorkflowBoundToEventType(WorkflowId workflowId, EventTypeId eventTypeId)
    : TelemetryEvent(("workflow_id", workflowId), ("event_type_id", eventTypeId));

public sealed class WorkflowUnboundFromEventType(WorkflowId workflowId, EventTypeId eventTypeId)
    : TelemetryEvent(("workflow_id", workflowId), ("event_type_id", eventTypeId));

public sealed class WorkflowReminderScheduled(WorkflowReminderId reminderId)
    : TelemetryEvent(("reminder_id", reminderId));

public sealed class WorkflowReminderDispatched(WorkflowReminderId reminderId)
    : TelemetryEvent(("reminder_id", reminderId));

public sealed class WorkflowReminderCancelled(WorkflowReminderId reminderId)
    : TelemetryEvent(("reminder_id", reminderId));

public sealed class ManagedEventTypeAssigned(EventTypeId parentId, EventTypeId childId, UserId memberUserId)
    : TelemetryEvent(("parent_event_type_id", parentId), ("child_event_type_id", childId), ("member_user_id", memberUserId));

public sealed class ManagedEventTypeUnassigned(EventTypeId parentId, EventTypeId childId, UserId memberUserId)
    : TelemetryEvent(("parent_event_type_id", parentId), ("child_event_type_id", childId), ("member_user_id", memberUserId));

public sealed class ManagedEventTypeSynced(EventTypeId parentId, int childCount)
    : TelemetryEvent(("parent_event_type_id", parentId), ("child_count", childCount));

public sealed class ManagedEventTypeLocksUpdated(EventTypeId parentId, int unlockedFieldCount)
    : TelemetryEvent(("parent_event_type_id", parentId), ("unlocked_field_count", unlockedFieldCount));

public sealed class ManagedEventTypeFieldOverrideRejected(EventTypeId childId, string fieldName)
    : TelemetryEvent(("child_event_type_id", childId), ("field_name", fieldName));

public sealed class CollectiveHostAdded(EventTypeId eventTypeId, UserId userId)
    : TelemetryEvent(("event_type_id", eventTypeId), ("user_id", userId));

public sealed class CollectiveHostRemoved(EventTypeId eventTypeId, UserId userId)
    : TelemetryEvent(("event_type_id", eventTypeId), ("user_id", userId));

public sealed class CollectiveSlotComputed(EventTypeId eventTypeId, int hostCount, int offeredCount)
    : TelemetryEvent(("event_type_id", eventTypeId), ("host_count", hostCount), ("offered_count", offeredCount));

public sealed class RoundRobinHostAdded(EventTypeId eventTypeId, UserId userId)
    : TelemetryEvent(("event_type_id", eventTypeId), ("user_id", userId));

public sealed class RoundRobinHostRemoved(EventTypeId eventTypeId, UserId userId)
    : TelemetryEvent(("event_type_id", eventTypeId), ("user_id", userId));

public sealed class RoundRobinHostUpdated(EventTypeId eventTypeId, UserId userId)
    : TelemetryEvent(("event_type_id", eventTypeId), ("user_id", userId));

public sealed class RoundRobinBookingReassigned(BookingId bookingId, UserId newOwnerUserId)
    : TelemetryEvent(("booking_id", bookingId), ("new_owner_user_id", newOwnerUserId));

public sealed class HashedLinkCreated(EventTypeId eventTypeId, HashedLinkId hashedLinkId)
    : TelemetryEvent(("event_type_id", eventTypeId), ("hashed_link_id", hashedLinkId));

public sealed class HashedLinkDeleted(EventTypeId eventTypeId, HashedLinkId hashedLinkId)
    : TelemetryEvent(("event_type_id", eventTypeId), ("hashed_link_id", hashedLinkId));

public sealed class TeamAssignmentUpdated(EventTypeId eventTypeId, bool assignAllTeamMembers)
    : TelemetryEvent(("event_type_id", eventTypeId), ("assign_all_team_members", assignAllTeamMembers));

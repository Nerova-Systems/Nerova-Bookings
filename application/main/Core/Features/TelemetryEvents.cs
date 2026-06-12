using Main.Features.Apps.Domain;
using Main.Features.Clients.Domain;
using Main.Features.DataImport.Domain;
using Main.Features.EventTypes.Domain;
using Main.Features.Receptionist.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Webhooks.Domain;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppMessaging.Domain;
using Main.Features.WhatsAppOnboarding.Domain;
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

public sealed class WhatsAppBusinessAccountOnboarded(WhatsAppBusinessAccountId whatsAppBusinessAccountId)
    : TelemetryEvent(("whats_app_business_account_id", whatsAppBusinessAccountId));

public sealed class WhatsAppBusinessAccountDisconnected(WhatsAppBusinessAccountId whatsAppBusinessAccountId)
    : TelemetryEvent(("whats_app_business_account_id", whatsAppBusinessAccountId));

public sealed class WhatsAppMessageReceived(WhatsAppMessageId whatsAppMessageId)
    : TelemetryEvent(("whats_app_message_id", whatsAppMessageId));

public sealed class WhatsAppMessageSent(WhatsAppMessageId whatsAppMessageId)
    : TelemetryEvent(("whats_app_message_id", whatsAppMessageId));

public sealed class ScheduleDeleted(ScheduleId scheduleId)
    : TelemetryEvent(("schedule_id", scheduleId));

public sealed class ScheduleDuplicated(ScheduleId sourceScheduleId, ScheduleId duplicateScheduleId)
    : TelemetryEvent(("source_schedule_id", sourceScheduleId), ("schedule_id", duplicateScheduleId));

public sealed class TravelScheduleCreated(TravelScheduleId travelScheduleId)
    : TelemetryEvent(("travel_schedule_id", travelScheduleId));

public sealed class TravelScheduleUpdated(TravelScheduleId travelScheduleId)
    : TelemetryEvent(("travel_schedule_id", travelScheduleId));

public sealed class TravelScheduleDeleted(TravelScheduleId travelScheduleId)
    : TelemetryEvent(("travel_schedule_id", travelScheduleId));

public sealed class OutOfOfficeCreated(OutOfOfficeId outOfOfficeId)
    : TelemetryEvent(("out_of_office_id", outOfOfficeId));

public sealed class OutOfOfficeUpdated(OutOfOfficeId outOfOfficeId)
    : TelemetryEvent(("out_of_office_id", outOfOfficeId));

public sealed class OutOfOfficeDeleted(OutOfOfficeId outOfOfficeId)
    : TelemetryEvent(("out_of_office_id", outOfOfficeId));

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

public sealed class AppInstallStarted(AppSlug slug)
    : TelemetryEvent(("app_slug", slug));

public sealed class AppInstallCompleted(AppSlug slug)
    : TelemetryEvent(("app_slug", slug));

public sealed class AppUninstalled(AppSlug slug)
    : TelemetryEvent(("app_slug", slug));

public sealed class WebhookCreated(WebhookId webhookId)
    : TelemetryEvent(("webhook_id", webhookId));

public sealed class WebhookUpdated(WebhookId webhookId)
    : TelemetryEvent(("webhook_id", webhookId));

public sealed class WebhookDeleted(WebhookId webhookId)
    : TelemetryEvent(("webhook_id", webhookId));

public sealed class WebhookDelivered(WebhookId webhookId, string eventType)
    : TelemetryEvent(("webhook_id", webhookId), ("event_type", eventType));

public sealed class WebhookDeliveryDeadLettered(WebhookId webhookId, string eventType)
    : TelemetryEvent(("webhook_id", webhookId), ("event_type", eventType));

public sealed class ClientUpdated(ClientId clientId)
    : TelemetryEvent(("client_id", clientId));

public sealed class ClientDeleted(ClientId clientId, bool bulkDeletion = false)
    : TelemetryEvent(("client_id", clientId), ("bulk_deletion", bulkDeletion));

public sealed class ClientsBulkDeleted(int count)
    : TelemetryEvent(("count", count));

public sealed class ReceptionistTurnCompleted(WhatsAppConversationId whatsAppConversationId, int toolCallCount, long inputTokens, long outputTokens, long latencyMs)
    : TelemetryEvent(("whats_app_conversation_id", whatsAppConversationId), ("tool_call_count", toolCallCount), ("input_tokens", inputTokens), ("output_tokens", outputTokens), ("latency_ms", latencyMs));

public sealed class ReceptionistEscalated(EscalationId escalationId, string reason)
    : TelemetryEvent(("escalation_id", escalationId), ("reason", reason));

public sealed class EscalationResolved(EscalationId escalationId, bool dismissed)
    : TelemetryEvent(("escalation_id", escalationId), ("dismissed", dismissed));

public sealed class ReceptionistSettingsUpdated(bool isEnabled, ReceptionistTone tone)
    : TelemetryEvent(("is_enabled", isEnabled), ("tone", tone));

public sealed class BookingCreatedByAgent(BookingId bookingId, EventTypeId eventTypeId)
    : TelemetryEvent(("booking_id", bookingId), ("event_type_id", eventTypeId));

public sealed class BookingCancelledByCustomer(BookingId bookingId)
    : TelemetryEvent(("booking_id", bookingId));

public sealed class BookingRescheduledByCustomer(BookingId oldBookingId, BookingId newBookingId)
    : TelemetryEvent(("old_booking_id", oldBookingId), ("booking_id", newBookingId));

public sealed class BookingDepositRequested(BookingId bookingId, EventTypeId eventTypeId, long amountMinorUnits)
    : TelemetryEvent(("booking_id", bookingId), ("event_type_id", eventTypeId), ("amount_minor_units", amountMinorUnits));

public sealed class DepositCollectedByAgent(BookingId bookingId)
    : TelemetryEvent(("booking_id", bookingId));

public sealed class ImportJobStarted(ImportJobId importJobId)
    : TelemetryEvent(("import_job_id", importJobId));

public sealed class ImportJobReadyForReview(ImportJobId importJobId, int rowsTotal, int rowsValid, int rowsDuplicate, int rowsInvalid)
    : TelemetryEvent(("import_job_id", importJobId), ("rows_total", rowsTotal), ("rows_valid", rowsValid), ("rows_duplicate", rowsDuplicate), ("rows_invalid", rowsInvalid));

public sealed class ImportJobCompleted(ImportJobId importJobId, int rowsTotal, int rowsCommitted, int rowsRejected)
    : TelemetryEvent(("import_job_id", importJobId), ("rows_total", rowsTotal), ("rows_committed", rowsCommitted), ("rows_rejected", rowsRejected));

public sealed class ImportJobRejected(ImportJobId importJobId)
    : TelemetryEvent(("import_job_id", importJobId));

public sealed class ClientsBulkImported(int createdCount, int mergedCount)
    : TelemetryEvent(("created_count", createdCount), ("merged_count", mergedCount));

public sealed class JobRunCompleted(string jobType, int level, string outcome)
    : TelemetryEvent(("job_type", jobType), ("level", level), ("outcome", outcome));

public sealed class JobSuggestionResolved(string jobType, bool approved)
    : TelemetryEvent(("job_type", jobType), ("approved", approved));

public sealed class AutonomyLevelChanged(string jobType, int fromLevel, int toLevel)
    : TelemetryEvent(("job_type", jobType), ("from_level", fromLevel), ("to_level", toLevel));

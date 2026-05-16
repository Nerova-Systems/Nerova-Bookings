using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
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

public sealed class EventTypeCreated(EventTypeId eventTypeId)
    : TelemetryEvent(("event_type_id", eventTypeId));

public sealed class EventTypeUpdated(EventTypeId eventTypeId)
    : TelemetryEvent(("event_type_id", eventTypeId));

public sealed class EventTypeDeleted(EventTypeId eventTypeId)
    : TelemetryEvent(("event_type_id", eventTypeId));

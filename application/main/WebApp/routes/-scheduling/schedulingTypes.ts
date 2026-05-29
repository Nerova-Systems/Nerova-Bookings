/* eslint-disable max-lines */
import type { Schemas } from "@/shared/lib/api/client";

export type Schedule = Schemas["ScheduleResponse"];
export type SchedulePayload = Schemas["CreateScheduleCommand"];
export type AvailabilityWindow = Schemas["AvailabilityWindowRequest"];
export type AvailabilityDateOverride = NonNullable<SchedulePayload["dateOverrides"]>[number];
export type AvailabilityOverrideWindow = AvailabilityDateOverride["windows"][number];
export type EventType = Schemas["EventTypeResponse"];
export type EventTypePayload = Schemas["CreateEventTypeCommand"] & { teamId?: string | null };
export type EventTypeUpdatePayload = Schemas["UpdateEventTypeCommand"];
export type CoreConnectorCalendar = { integration: string; externalId: string; credentialId?: string | null };
export type CoreConnectorConferencing = { app: string; credentialId?: string | null };
export type EventTypeSettings = NonNullable<EventTypePayload["settings"]> & {
  destinationCalendar?: CoreConnectorCalendar | null;
  defaultConferencing?: CoreConnectorConferencing | null;
};
export type ApiValidationError = Schemas["HttpValidationProblemDetails"] | null | undefined;

export function newSchedulePayload(isDefault = false): SchedulePayload {
  return {
    name: isDefault ? "Default schedule" : "Working hours",
    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
    isDefault,
    availabilityWindows: [{ days: [1, 2, 3, 4, 5], startMinute: 540, endMinute: 1020 }],
    dateOverrides: []
  };
}

export function scheduleToPayload(schedule: Schedule): SchedulePayload {
  return {
    name: schedule.name,
    timeZone: schedule.timeZone,
    isDefault: schedule.isDefault,
    availabilityWindows: schedule.availabilityWindows.map((window) => ({
      days: [...window.days],
      startMinute: window.startMinute,
      endMinute: window.endMinute
    })),
    dateOverrides: schedule.dateOverrides.map((dateOverride) => ({
      date: dateOverride.date,
      windows: dateOverride.windows.map((window) => ({ ...window }))
    }))
  };
}

export function newEventTypePayload(scheduleId: string): EventTypePayload {
  return {
    title: "",
    slug: "",
    description: null,
    durationMinutes: 30,
    hidden: false,
    scheduleId,
    beforeEventBufferMinutes: 0,
    afterEventBufferMinutes: 0,
    slotIntervalMinutes: 30,
    minimumBookingNoticeMinutes: 60,
    locationType: "link",
    locationValue: "",
    settings: null
  };
}

export function isSchedulePayloadSubmittable(value: SchedulePayload) {
  return (
    value.name.trim().length > 0 &&
    value.timeZone.trim().length > 0 &&
    value.availabilityWindows.length > 0 &&
    getOverlappingAvailabilityWindowIndexes(value.availabilityWindows).size === 0 &&
    getOverlappingAvailabilityOverrideIndexes(value.dateOverrides ?? []).size === 0 &&
    value.availabilityWindows.every(
      (window) =>
        window.days.length > 0 &&
        window.days.every((day) => day >= 0 && day <= 6) &&
        window.startMinute >= 0 &&
        window.endMinute <= 1440 &&
        window.startMinute < window.endMinute
    ) &&
    (value.dateOverrides ?? []).every((dateOverride) =>
      dateOverride.windows.every(
        (window) => window.startMinute >= 0 && window.endMinute <= 1440 && window.startMinute < window.endMinute
      )
    )
  );
}

export function isEventTypePayloadSubmittable(value: EventTypePayload) {
  return (
    value.title.trim().length > 0 &&
    value.slug.trim().length > 0 &&
    value.scheduleId.trim().length > 0 &&
    value.durationMinutes >= 5 &&
    value.slotIntervalMinutes >= 5 &&
    value.minimumBookingNoticeMinutes >= 0 &&
    value.beforeEventBufferMinutes >= 0 &&
    value.afterEventBufferMinutes >= 0
  );
}

export function eventTypeToPayload(eventType: EventType): EventTypePayload {
  return {
    title: eventType.title,
    slug: eventType.slug,
    description: eventType.description,
    durationMinutes: eventType.durationMinutes,
    hidden: eventType.hidden,
    scheduleId: eventType.scheduleId,
    beforeEventBufferMinutes: eventType.beforeEventBufferMinutes,
    afterEventBufferMinutes: eventType.afterEventBufferMinutes,
    slotIntervalMinutes: eventType.slotIntervalMinutes,
    minimumBookingNoticeMinutes: eventType.minimumBookingNoticeMinutes,
    locationType: eventType.locationType,
    locationValue: eventType.locationValue,
    settings: eventType.settings,
    teamId: (eventType as any).teamId
  };
}

export function eventTypeToUpdatePayload(
  eventTypeId: EventType["id"],
  payload: EventTypePayload
): EventTypeUpdatePayload {
  return {
    ...payload,
    id: eventTypeId
  };
}

export function getEventTypeSettings(payload: EventTypePayload): EventTypeSettings {
  const settings = payload.settings as EventTypeSettings | null | undefined;
  const primaryLocation = payload.locationType
    ? [
        {
          type: payload.locationType,
          value: payload.locationValue?.trim() || null,
          displayLocationPubliclyToTeam: false
        }
      ]
    : [];
  const durationOptions =
    settings?.durationOptions && settings.durationOptions.length > 0
      ? settings.durationOptions
      : [payload.durationMinutes];

  return {
    durationOptions,
    locations:
      settings?.locations && settings.locations.length > 0
        ? settings.locations.map((location) => ({
            type: location.type,
            value: location.value,
            displayLocationPubliclyToTeam: location.displayLocationPubliclyToTeam ?? false
          }))
        : primaryLocation,
    bookingFields: settings?.bookingFields ?? [],
    bookerLayout: settings?.bookerLayout?.trim() || "month",
    eventColor: settings?.eventColor?.trim() || null,
    bookingWindow: {
      rollingWindowDays: settings?.bookingWindow?.rollingWindowDays ?? null,
      fixedStartDate: settings?.bookingWindow?.fixedStartDate ?? null,
      fixedEndDate: settings?.bookingWindow?.fixedEndDate ?? null
    },
    limits: {
      maxBookingsPerDay: settings?.limits?.maxBookingsPerDay ?? null,
      maxBookingsPerWeek: settings?.limits?.maxBookingsPerWeek ?? null,
      maxBookingsPerMonth: settings?.limits?.maxBookingsPerMonth ?? null,
      maxBookingsPerYear: settings?.limits?.maxBookingsPerYear ?? null,
      maxBookingDurationMinutesPerDay: settings?.limits?.maxBookingDurationMinutesPerDay ?? null,
      maxBookingDurationPerDay: settings?.limits?.maxBookingDurationPerDay ?? null,
      maxBookingDurationPerWeek: settings?.limits?.maxBookingDurationPerWeek ?? null,
      maxBookingDurationPerMonth: settings?.limits?.maxBookingDurationPerMonth ?? null,
      maxBookingDurationPerYear: settings?.limits?.maxBookingDurationPerYear ?? null,
      maxActiveBookingsPerBooker: settings?.limits?.maxActiveBookingsPerBooker ?? null,
      maxActiveBookingPerBookerOfferReschedule: settings?.limits?.maxActiveBookingPerBookerOfferReschedule ?? false,
      firstAvailableSlotMinutes: settings?.limits?.firstAvailableSlotMinutes ?? null,
      offsetStartMinutes: settings?.limits?.offsetStartMinutes ?? null,
      onlyShowFirstAvailableSlot: settings?.limits?.onlyShowFirstAvailableSlot ?? false,
      showOptimizedSlots: settings?.limits?.showOptimizedSlots ?? false
    },
    confirmationPolicy: {
      requiresConfirmation: settings?.confirmationPolicy?.requiresConfirmation ?? false,
      requiresBookerEmailVerification: settings?.confirmationPolicy?.requiresBookerEmailVerification ?? false,
      blockSlotWhilePending: settings?.confirmationPolicy?.blockSlotWhilePending ?? false,
      requiresConfirmationForFreeEmail: settings?.confirmationPolicy?.requiresConfirmationForFreeEmail ?? false,
      requiresCancellationReason: settings?.confirmationPolicy?.requiresCancellationReason ?? false
    },
    recurrence: settings?.recurrence ?? null,
    seats: {
      enabled: settings?.seats?.enabled ?? false,
      capacity: settings?.seats?.capacity ?? null,
      showAttendeeInfo: settings?.seats?.showAttendeeInfo ?? false
    },
    privateLinks: settings?.privateLinks ?? [],
    cancellationPolicy: {
      allowCancellation: settings?.cancellationPolicy?.allowCancellation ?? true,
      minimumNoticeMinutes: settings?.cancellationPolicy?.minimumNoticeMinutes ?? null
    },
    reschedulePolicy: {
      allowReschedule: settings?.reschedulePolicy?.allowReschedule ?? true,
      minimumNoticeMinutes: settings?.reschedulePolicy?.minimumNoticeMinutes ?? null,
      allowReschedulingPastBookings: settings?.reschedulePolicy?.allowReschedulingPastBookings ?? false,
      allowReschedulingCancelledBookings: settings?.reschedulePolicy?.allowReschedulingCancelledBookings ?? false
    },
    redirects: {
      successUrl: settings?.redirects?.successUrl ?? null,
      cancellationUrl: settings?.redirects?.cancellationUrl ?? null
    },
    interfaceLanguage: settings?.interfaceLanguage?.trim() || null,
    metadata: settings?.metadata ?? {},
    instantMeeting: {
      expiryTimeOffsetInSeconds: settings?.instantMeeting?.expiryTimeOffsetInSeconds ?? null,
      instantMeetingScheduleId: settings?.instantMeeting?.instantMeetingScheduleId ?? null,
      parameters: settings?.instantMeeting?.parameters ?? null
    },
    aiVoiceAgent: {
      enabled: settings?.aiVoiceAgent?.enabled ?? false,
      agentConfig: settings?.aiVoiceAgent?.agentConfig ?? null
    },
    teamAssignment: {
      assignRrMembersUsingSegment: settings?.teamAssignment?.assignRrMembersUsingSegment ?? false,
      rrSegmentQueryValue: settings?.teamAssignment?.rrSegmentQueryValue ?? null,
      isRrWeightsEnabled: settings?.teamAssignment?.isRrWeightsEnabled ?? false,
      maxLeadThreshold: settings?.teamAssignment?.maxLeadThreshold ?? null,
      includeNoShowInRrCalculation: settings?.teamAssignment?.includeNoShowInRrCalculation ?? false,
      rescheduleWithSameRoundRobinHost: settings?.teamAssignment?.rescheduleWithSameRoundRobinHost ?? false,
      rrHostSubsetEnabled: settings?.teamAssignment?.rrHostSubsetEnabled ?? false,
      hostGroups: settings?.teamAssignment?.hostGroups ?? []
    },
    timezone: {
      timeZone: settings?.timezone?.timeZone ?? null,
      lockTimeZoneToggleOnBookingPage: settings?.timezone?.lockTimeZoneToggleOnBookingPage ?? false,
      lockedTimeZone: settings?.timezone?.lockedTimeZone ?? null,
      useBookerTimezone: settings?.timezone?.useBookerTimezone ?? false,
      restrictionScheduleId: settings?.timezone?.restrictionScheduleId ?? null
    },
    privacy: {
      disableGuests: settings?.privacy?.disableGuests ?? false,
      hideCalendarNotes: settings?.privacy?.hideCalendarNotes ?? false,
      hideCalendarEventDetails: settings?.privacy?.hideCalendarEventDetails ?? false
    },
    email: {
      eventName: settings?.email?.eventName ?? null,
      customReplyToEmail: settings?.email?.customReplyToEmail ?? null
    },
    enablePerHostLocations: settings?.enablePerHostLocations ?? false,
    selectedCalendars: settings?.selectedCalendars ?? [],
    destinationCalendar: settings?.destinationCalendar ?? null,
    defaultConferencing: settings?.defaultConferencing ?? null
  };
}

export function updateEventTypeSettings(
  payload: EventTypePayload,
  updater: (settings: EventTypeSettings) => EventTypeSettings
): EventTypePayload {
  return {
    ...payload,
    settings: updater(getEventTypeSettings(payload))
  };
}

export function updateEventTypeSettingsSection<Key extends keyof EventTypeSettings>(
  payload: EventTypePayload,
  key: Key,
  updater: (section: EventTypeSettings[Key], settings: EventTypeSettings) => EventTypeSettings[Key]
): EventTypePayload {
  return updateEventTypeSettings(payload, (settings) => ({
    ...settings,
    [key]: updater(settings[key], settings)
  }));
}

export function slugify(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

export function formatMinutes(totalMinutes: number) {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
}

export function formatAvailabilityWindows(
  windows: AvailabilityWindow[],
  weekdayLabels: Record<number, string>,
  emptyLabel: string
) {
  if (windows.length === 0) return emptyLabel;

  return windows
    .map((window) => {
      const days = formatWeekdayRanges(window.days, weekdayLabels);
      return `${days}: ${formatMinutes(window.startMinute)}-${formatMinutes(window.endMinute)}`;
    })
    .join("; ");
}

function formatWeekdayRanges(days: number[], weekdayLabels: Record<number, string>) {
  const sortedDays = [...new Set(days)].sort((left, right) => left - right);
  const ranges: string[] = [];

  for (let index = 0; index < sortedDays.length; index++) {
    const rangeStart = sortedDays[index];
    let rangeEnd = rangeStart;

    while (sortedDays[index + 1] === rangeEnd + 1) {
      rangeEnd = sortedDays[index + 1];
      index++;
    }

    ranges.push(
      rangeStart === rangeEnd ? weekdayLabels[rangeStart] : `${weekdayLabels[rangeStart]}-${weekdayLabels[rangeEnd]}`
    );
  }

  return ranges.join(", ");
}

export function getOverlappingAvailabilityWindowIndexes(windows: AvailabilityWindow[]) {
  const overlappingIndexes = new Set<number>();

  windows.forEach((window, index) => {
    window.days.forEach((day) => {
      windows.forEach((comparisonWindow, comparisonIndex) => {
        if (comparisonIndex <= index || !comparisonWindow.days.includes(day)) return;

        const overlaps =
          window.startMinute < comparisonWindow.endMinute && comparisonWindow.startMinute < window.endMinute;
        if (overlaps) {
          overlappingIndexes.add(index);
          overlappingIndexes.add(comparisonIndex);
        }
      });
    });
  });

  return overlappingIndexes;
}

export function getOverlappingAvailabilityOverrideIndexes(dateOverrides: AvailabilityDateOverride[]) {
  const overlappingIndexes = new Set<string>();

  dateOverrides.forEach((dateOverride) => {
    dateOverride.windows.forEach((window, index) => {
      dateOverride.windows.forEach((comparisonWindow, comparisonIndex) => {
        if (comparisonIndex <= index) return;

        const overlaps =
          window.startMinute < comparisonWindow.endMinute && comparisonWindow.startMinute < window.endMinute;
        if (overlaps) {
          overlappingIndexes.add(`${dateOverride.date}-${index}`);
          overlappingIndexes.add(`${dateOverride.date}-${comparisonIndex}`);
        }
      });
    });
  });

  return overlappingIndexes;
}

export function parseTime(value: string, fallback: number) {
  const match = /^(\d{1,2}):(\d{2})$/.exec(value.trim());
  if (!match) return fallback;

  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (hours < 0 || hours > 24 || minutes < 0 || minutes > 59) return fallback;

  return Math.min(hours * 60 + minutes, 1440);
}

export function getApiErrorMessages(error: ApiValidationError) {
  return [error?.detail, ...Object.values(error?.errors ?? {}).flat()].filter(Boolean);
}

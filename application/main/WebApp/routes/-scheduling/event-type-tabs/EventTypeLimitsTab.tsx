import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { getEventTypeSettings, updateEventTypeSettingsSection } from "../schedulingTypes";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeLimitsTab({ value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const updateBookingWindow = (bookingWindow: Partial<ReturnType<typeof getEventTypeSettings>["bookingWindow"]>) => {
    onChange(
      updateEventTypeSettingsSection(value, "bookingWindow", (currentBookingWindow) => ({
        ...currentBookingWindow,
        ...bookingWindow
      }))
    );
  };
  const updateLimits = (limits: Partial<ReturnType<typeof getEventTypeSettings>["limits"]>) => {
    onChange(
      updateEventTypeSettingsSection(value, "limits", (currentLimits) => ({
        ...currentLimits,
        ...limits
      }))
    );
  };

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <div className="grid gap-5">
        <EventTypeTabSection
          title={<Trans>Booking limits</Trans>}
          description={<Trans>Control how soon bookings can be made and how the schedule is divided into slots.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <NumberField
              name="slotIntervalMinutes"
              label={t`Slot interval`}
              minValue={5}
              maxValue={1440}
              value={value.slotIntervalMinutes}
              onChange={(slotIntervalMinutes) => onChange({ ...value, slotIntervalMinutes: slotIntervalMinutes ?? 30 })}
            />
            <NumberField
              name="minimumBookingNoticeMinutes"
              label={t`Notice`}
              minValue={0}
              maxValue={525600}
              value={value.minimumBookingNoticeMinutes}
              onChange={(minimumBookingNoticeMinutes) =>
                onChange({ ...value, minimumBookingNoticeMinutes: minimumBookingNoticeMinutes ?? 0 })
              }
            />
            <NumberField
              name="rollingWindowDays"
              label={t`Rolling window`}
              minValue={0}
              maxValue={3650}
              allowEmpty={true}
              value={settings.bookingWindow.rollingWindowDays ?? undefined}
              onChange={(rollingWindowDays) => updateBookingWindow({ rollingWindowDays })}
            />
            <NumberField
              name="firstAvailableSlotMinutes"
              label={t`First slot only`}
              minValue={0}
              maxValue={525600}
              allowEmpty={true}
              value={settings.limits.firstAvailableSlotMinutes ?? undefined}
              onChange={(firstAvailableSlotMinutes) => updateLimits({ firstAvailableSlotMinutes })}
            />
            <NumberField
              name="offsetStartMinutes"
              label={t`Offset start`}
              minValue={0}
              maxValue={1440}
              allowEmpty={true}
              value={settings.limits.offsetStartMinutes ?? undefined}
              onChange={(offsetStartMinutes) => updateLimits({ offsetStartMinutes })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Capacity limits</Trans>}
          description={<Trans>Limit booking volume across the event type and individual bookers.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <NumberField
              name="maxBookingsPerDay"
              label={t`Bookings per day`}
              minValue={0}
              maxValue={10000}
              allowEmpty={true}
              value={settings.limits.maxBookingsPerDay ?? undefined}
              onChange={(maxBookingsPerDay) => updateLimits({ maxBookingsPerDay })}
            />
            <NumberField
              name="maxBookingDurationMinutesPerDay"
              label={t`Booked minutes per day`}
              minValue={0}
              maxValue={525600}
              allowEmpty={true}
              value={settings.limits.maxBookingDurationMinutesPerDay ?? undefined}
              onChange={(maxBookingDurationMinutesPerDay) => updateLimits({ maxBookingDurationMinutesPerDay })}
            />
            <NumberField
              name="maxActiveBookingsPerBooker"
              label={t`Active bookings per booker`}
              minValue={0}
              maxValue={10000}
              allowEmpty={true}
              value={settings.limits.maxActiveBookingsPerBooker ?? undefined}
              onChange={(maxActiveBookingsPerBooker) => updateLimits({ maxActiveBookingsPerBooker })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Buffers</Trans>}
          description={<Trans>Reserve time before and after every booking.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <NumberField
              name="beforeEventBufferMinutes"
              label={t`Before buffer`}
              minValue={0}
              maxValue={1440}
              value={value.beforeEventBufferMinutes}
              onChange={(beforeEventBufferMinutes) =>
                onChange({ ...value, beforeEventBufferMinutes: beforeEventBufferMinutes ?? 0 })
              }
            />
            <NumberField
              name="afterEventBufferMinutes"
              label={t`After buffer`}
              minValue={0}
              maxValue={1440}
              value={value.afterEventBufferMinutes}
              onChange={(afterEventBufferMinutes) =>
                onChange({ ...value, afterEventBufferMinutes: afterEventBufferMinutes ?? 0 })
              }
            />
          </div>
        </EventTypeTabSection>
      </div>
    </FormValidationContext.Provider>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeLimitsTab({ value, onChange, error }: EventTypeTabProps) {
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

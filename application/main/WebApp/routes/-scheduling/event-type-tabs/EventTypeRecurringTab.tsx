import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { SwitchField } from "@repo/ui/components/SwitchField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { getEventTypeSettings, updateEventTypeSettings } from "../schedulingTypes";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeRecurringTab({ value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const recurrence = settings.recurrence;
  const seatsEnabled = settings.seats.enabled;
  const recurrenceDisabled = seatsEnabled && recurrence === null;
  const frequencyOptions = [
    { value: "daily", label: t`Daily` },
    { value: "weekly", label: t`Weekly` },
    { value: "monthly", label: t`Monthly` },
    { value: "yearly", label: t`Yearly` }
  ];

  const updateRecurrence = (nextRecurrence: typeof recurrence) => {
    onChange(
      updateEventTypeSettings(value, (nextSettings) => ({
        ...nextSettings,
        recurrence: nextRecurrence
      }))
    );
  };

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <EventTypeTabSection
        title={<Trans>Recurring events</Trans>}
        description={
          seatsEnabled ? (
            <Trans>Recurring events cannot be enabled while seats are enabled.</Trans>
          ) : (
            <Trans>Let bookers reserve a series of repeated bookings.</Trans>
          )
        }
      >
        <SwitchField
          name="recurrenceEnabled"
          label={t`Recurring event`}
          checked={recurrence !== null}
          disabled={recurrenceDisabled}
          onCheckedChange={(enabled) =>
            updateRecurrence(enabled ? (recurrence ?? { frequency: "weekly", interval: 1, count: null }) : null)
          }
        />
        {recurrence && (
          <div className="grid gap-4 md:grid-cols-3">
            <SelectField
              name="recurrenceFrequency"
              label={t`Frequency`}
              items={frequencyOptions}
              value={recurrence.frequency}
              onValueChange={(frequency) => updateRecurrence({ ...recurrence, frequency: frequency ?? "weekly" })}
            >
              <SelectTrigger>
                <SelectValue>
                  {(frequency: string) => frequencyOptions.find((item) => item.value === frequency)?.label}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {frequencyOptions.map((frequency) => (
                  <SelectItem key={frequency.value} value={frequency.value}>
                    {frequency.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </SelectField>
            <NumberField
              name="recurrenceInterval"
              label={t`Interval`}
              minValue={1}
              maxValue={365}
              value={recurrence.interval}
              onChange={(interval) => updateRecurrence({ ...recurrence, interval: interval ?? 1 })}
            />
            <NumberField
              name="recurrenceCount"
              label={t`Occurrences`}
              minValue={1}
              maxValue={365}
              allowEmpty={true}
              value={recurrence.count ?? undefined}
              onChange={(count) => updateRecurrence({ ...recurrence, count })}
            />
          </div>
        )}
      </EventTypeTabSection>
    </FormValidationContext.Provider>
  );
}

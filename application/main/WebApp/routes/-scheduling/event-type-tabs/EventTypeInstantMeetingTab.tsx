import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { getEventTypeSettings, updateEventTypeSettingsSection } from "../schedulingTypes";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeInstantMeetingTab({ value, schedules, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const instantMeeting = settings.instantMeeting;
  const updateInstantMeeting = (partial: Partial<typeof instantMeeting>) => {
    onChange(
      updateEventTypeSettingsSection(value, "instantMeeting", (current) => ({
        ...current,
        ...partial
      }))
    );
  };
  const scheduleOptions = [
    { value: "", label: t`No specific schedule` },
    ...schedules.map((schedule) => ({ value: schedule.id, label: schedule.name }))
  ];

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <EventTypeTabSection
        title={<Trans>Instant meeting</Trans>}
        description={
          <Trans>
            Let qualified bookers start a meeting immediately. Hosts get an instant alert and have a short window to
            accept before the request expires.
          </Trans>
        }
      >
        <div className="grid gap-4 md:grid-cols-2">
          <NumberField
            name="expiryTimeOffsetInSeconds"
            label={t`Request expires after`}
            minValue={10}
            maxValue={3600}
            allowEmpty={true}
            value={instantMeeting.expiryTimeOffsetInSeconds ?? undefined}
            onChange={(expiryTimeOffsetInSeconds) => updateInstantMeeting({ expiryTimeOffsetInSeconds })}
          />
          <SelectField
            name="instantMeetingScheduleId"
            label={t`Instant meeting schedule`}
            items={scheduleOptions}
            value={instantMeeting.instantMeetingScheduleId ?? ""}
            onValueChange={(scheduleId) =>
              updateInstantMeeting({
                instantMeetingScheduleId: scheduleId && scheduleId.length > 0 ? scheduleId : null
              })
            }
          >
            <SelectTrigger>
              <SelectValue>
                {(scheduleId: string) => scheduleOptions.find((item) => item.value === scheduleId)?.label}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {scheduleOptions.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </SelectField>
        </div>
      </EventTypeTabSection>
    </FormValidationContext.Provider>
  );
}

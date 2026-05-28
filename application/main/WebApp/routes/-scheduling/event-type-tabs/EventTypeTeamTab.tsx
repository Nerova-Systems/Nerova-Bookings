import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextField } from "@repo/ui/components/TextField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { getEventTypeSettings, updateEventTypeSettingsSection } from "../schedulingTypes";
import { EventTypeHostPicker } from "./EventTypeHostPicker";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeTeamTab({ eventTypeId, value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const teamAssignment = settings.teamAssignment;
  const updateTeamAssignment = (partial: Partial<typeof teamAssignment>) => {
    onChange(
      updateEventTypeSettingsSection(value, "teamAssignment", (current) => ({
        ...current,
        ...partial
      }))
    );
  };

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <div className="grid gap-5">
        <EventTypeTabSection
          title={<Trans>Round robin</Trans>}
          description={<Trans>Distribute bookings across hosts based on weighting and lead history.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="isRrWeightsEnabled"
              label={t`Use weights`}
              checked={teamAssignment.isRrWeightsEnabled}
              onCheckedChange={(isRrWeightsEnabled) => updateTeamAssignment({ isRrWeightsEnabled })}
            />
            <SwitchField
              name="includeNoShowInRrCalculation"
              label={t`Count no-shows`}
              checked={teamAssignment.includeNoShowInRrCalculation}
              onCheckedChange={(includeNoShowInRrCalculation) => updateTeamAssignment({ includeNoShowInRrCalculation })}
            />
            <SwitchField
              name="rescheduleWithSameRoundRobinHost"
              label={t`Reschedule with same host`}
              checked={teamAssignment.rescheduleWithSameRoundRobinHost}
              onCheckedChange={(rescheduleWithSameRoundRobinHost) =>
                updateTeamAssignment({ rescheduleWithSameRoundRobinHost })
              }
            />
            <NumberField
              name="maxLeadThreshold"
              label={t`Max lead threshold`}
              minValue={0}
              maxValue={10000}
              allowEmpty={true}
              value={teamAssignment.maxLeadThreshold ?? undefined}
              onChange={(maxLeadThreshold) => updateTeamAssignment({ maxLeadThreshold })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Host segmentation</Trans>}
          description={<Trans>Limit which hosts can be assigned using a segment expression.</Trans>}
        >
          <div className="grid gap-4">
            <SwitchField
              name="assignRrMembersUsingSegment"
              label={t`Use segment query`}
              checked={teamAssignment.assignRrMembersUsingSegment}
              onCheckedChange={(assignRrMembersUsingSegment) => updateTeamAssignment({ assignRrMembersUsingSegment })}
            />
            <TextField
              name="rrSegmentQueryValue"
              label={t`Segment query`}
              disabled={!teamAssignment.assignRrMembersUsingSegment}
              value={teamAssignment.rrSegmentQueryValue ?? ""}
              onChange={(rrSegmentQueryValue) =>
                updateTeamAssignment({ rrSegmentQueryValue: rrSegmentQueryValue || null })
              }
            />
            <SwitchField
              name="rrHostSubsetEnabled"
              label={t`Host subset rotation`}
              checked={teamAssignment.rrHostSubsetEnabled}
              onCheckedChange={(rrHostSubsetEnabled) => updateTeamAssignment({ rrHostSubsetEnabled })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Hosts</Trans>}
          description={<Trans>Add, remove, and weight individual hosts and host groups.</Trans>}
        >
          <EventTypeHostPicker
            eventTypeId={eventTypeId}
            hostGroups={teamAssignment.hostGroups}
            onChange={(hostGroups) => updateTeamAssignment({ hostGroups })}
          />
        </EventTypeTabSection>
      </div>
    </FormValidationContext.Provider>
  );
}

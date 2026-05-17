import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextField } from "@repo/ui/components/TextField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { LocationTypeSelect } from "../LocationTypeSelect";
import { DisabledFeatureRow, EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeAdvancedTab({ value, onChange, error }: EventTypeTabProps) {
  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <div className="grid gap-5">
        <EventTypeTabSection
          title={<Trans>Visibility</Trans>}
          description={<Trans>Control whether this booking page is listed publicly.</Trans>}
        >
          <SwitchField
            name="hidden"
            label={t`Hidden`}
            checked={value.hidden}
            onCheckedChange={(hidden) => onChange({ ...value, hidden })}
          />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Location</Trans>}
          description={<Trans>Choose how guests will join or where they should arrive.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <LocationTypeSelect
              value={value.locationType ?? ""}
              onChange={(locationType) => onChange({ ...value, locationType })}
            />
            <TextField
              name="locationValue"
              label={t`Location`}
              value={value.locationValue ?? ""}
              onChange={(locationValue) => onChange({ ...value, locationValue: locationValue || null })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Additional settings</Trans>}
          description={<Trans>These settings will become editable when backend support is available.</Trans>}
        >
          <DisabledFeatureRow
            title={<Trans>Requires confirmation</Trans>}
            description={<Trans>Hold new bookings for manual approval before they are confirmed.</Trans>}
          />
          <DisabledFeatureRow
            title={<Trans>Private links</Trans>}
            description={<Trans>Limit booking access to generated private links.</Trans>}
          />
          <DisabledFeatureRow
            title={<Trans>Redirect after booking</Trans>}
            description={<Trans>Send guests to a custom page after they complete a booking.</Trans>}
          />
          <DisabledFeatureRow
            title={<Trans>Language</Trans>}
            description={<Trans>Set a specific language for this event type booking page.</Trans>}
          />
        </EventTypeTabSection>
      </div>
    </FormValidationContext.Provider>
  );
}

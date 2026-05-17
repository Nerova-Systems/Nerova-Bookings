import { Trans } from "@lingui/react/macro";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { DisabledFeatureRow, EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeRecurringTab(_props: EventTypeTabProps) {
  return (
    <EventTypeTabSection
      title={<Trans>Recurring events</Trans>}
      description={<Trans>Recurring booking rules are planned but are not connected to backend settings yet.</Trans>}
    >
      <DisabledFeatureRow
        title={<Trans>Repeat interval</Trans>}
        description={<Trans>Let guests book a series of events from one booking page.</Trans>}
      />
      <DisabledFeatureRow
        title={<Trans>Recurring limits</Trans>}
        description={<Trans>Limit how many events can be created in a recurring series.</Trans>}
      />
      <DisabledFeatureRow
        title={<Trans>Series cancellation</Trans>}
        description={<Trans>Control whether guests can cancel one event or the full series.</Trans>}
      />
    </EventTypeTabSection>
  );
}

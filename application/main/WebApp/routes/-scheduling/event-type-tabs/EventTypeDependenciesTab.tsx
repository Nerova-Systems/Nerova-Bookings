import { Trans } from "@lingui/react/macro";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { DisabledFeatureRow, EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeDependenciesTab(_props: EventTypeTabProps) {
  return (
    <EventTypeTabSection
      title={<Trans>Dependencies</Trans>}
      description={<Trans>Dependency rules are placeholders until event type relationships are supported.</Trans>}
    >
      <DisabledFeatureRow
        title={<Trans>Requires another booking</Trans>}
        description={<Trans>Make this event type available only after a related booking exists.</Trans>}
      />
      <DisabledFeatureRow
        title={<Trans>Blocks other event types</Trans>}
        description={<Trans>Prevent selected event types from being booked when this one is active.</Trans>}
      />
      <DisabledFeatureRow
        title={<Trans>Managed relationship rules</Trans>}
        description={<Trans>Coordinate event type dependencies without calling unavailable APIs.</Trans>}
      />
    </EventTypeTabSection>
  );
}

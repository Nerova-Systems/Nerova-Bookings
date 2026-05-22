import { Trans } from "@lingui/react/macro";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { DisabledFeatureRow, EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeAppsTab(_props: EventTypeTabProps) {
  return (
    <EventTypeTabSection
      title={<Trans>Apps</Trans>}
      description={<Trans>Connect installed apps to this event type to add payments, conferencing, and more.</Trans>}
    >
      {/* TODO: app marketplace integration not yet shipped. */}
      <DisabledFeatureRow
        title={<Trans>App marketplace</Trans>}
        description={<Trans>Per-event-type app configuration will be available in a future release.</Trans>}
      />
    </EventTypeTabSection>
  );
}

import { Trans } from "@lingui/react/macro";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { DisabledFeatureRow, EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeWebhooksTab(_props: EventTypeTabProps) {
  return (
    <EventTypeTabSection
      title={<Trans>Webhooks</Trans>}
      description={
        <Trans>
          Send HTTP requests to external systems when bookings on this event type are created, cancelled, or
          rescheduled.
        </Trans>
      }
    >
      {/* TODO: backend webhook endpoints not yet shipped. */}
      <DisabledFeatureRow
        title={<Trans>Outgoing webhooks</Trans>}
        description={<Trans>Webhook subscriptions will be available in a future release.</Trans>}
      />
    </EventTypeTabSection>
  );
}

import { Trans } from "@lingui/react/macro";
import { Checkbox } from "@repo/ui/components/Checkbox";
import { Label } from "@repo/ui/components/Label";

import type { WebhookEventType } from "@/shared/lib/api/client";

import { getWebhookEventTypeLabel, WEBHOOK_EVENT_TYPE_ORDER } from "./webhookTypes";

interface EventSubscriptionsFieldProps {
  value: ReadonlySet<WebhookEventType>;
  onChange: (next: Set<WebhookEventType>) => void;
  disabled?: boolean;
}

/** Multi-select checkboxes for picking the webhook event subscriptions. */
export function EventSubscriptionsField({ value, onChange, disabled }: Readonly<EventSubscriptionsFieldProps>) {
  const toggle = (eventType: WebhookEventType, checked: boolean) => {
    const next = new Set(value);
    if (checked) {
      next.add(eventType);
    } else {
      next.delete(eventType);
    }
    onChange(next);
  };

  return (
    <fieldset className="flex flex-col gap-3" disabled={disabled}>
      <legend className="text-sm font-medium text-foreground">
        <Trans>Event subscriptions</Trans>
      </legend>
      <div className="grid gap-2 sm:grid-cols-2">
        {WEBHOOK_EVENT_TYPE_ORDER.map((eventType) => {
          const inputId = `webhook-event-${eventType}`;
          return (
            <label
              key={eventType}
              htmlFor={inputId}
              className="flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2 hover:bg-muted/50"
            >
              <Checkbox
                id={inputId}
                checked={value.has(eventType)}
                onCheckedChange={(checked) => toggle(eventType, checked === true)}
                disabled={disabled}
              />
              <Label htmlFor={inputId} className="cursor-pointer text-sm">
                {getWebhookEventTypeLabel(eventType)}
              </Label>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextField } from "@repo/ui/components/TextField";

import { api, type WebhookEventType } from "@/shared/lib/api/client";

import { EventSubscriptionsField } from "./EventSubscriptionsField";
import { isValidTargetUrl } from "./webhookTypes";

const UNSCOPED_VALUE = "__all__";

export interface WebhookFormState {
  targetUrl: string;
  active: boolean;
  eventSubscriptions: Set<WebhookEventType>;
  eventTypeId: string | null;
}

interface WebhookFormFieldsProps {
  state: WebhookFormState;
  onChange: (next: WebhookFormState) => void;
  /** When true, the EventType scope dropdown is hidden (e.g. when editing — backend update has no scope field). */
  hideEventTypeScope?: boolean;
  disabled?: boolean;
}

/** Form fields shared between the create dialog and the detail page edit form. */
export function WebhookFormFields({ state, onChange, hideEventTypeScope, disabled }: Readonly<WebhookFormFieldsProps>) {
  const { data: eventTypesData } = api.useQuery(
    "get",
    "/api/event-types",
    {},
    { enabled: hideEventTypeScope !== true }
  );
  const eventTypes = hideEventTypeScope === true ? [] : (eventTypesData?.eventTypes ?? []);

  const urlError =
    state.targetUrl.trim().length > 0 && !isValidTargetUrl(state.targetUrl) ? t`Enter a valid http(s) URL.` : undefined;

  const scopeItems = [
    { value: UNSCOPED_VALUE, label: t`All services` },
    ...eventTypes.map((eventType) => ({ value: eventType.id, label: eventType.title }))
  ];

  return (
    <div className="flex flex-col gap-4">
      <TextField
        name="targetUrl"
        label={t`Target URL`}
        type="url"
        placeholder="https://example.com/webhooks/nerova"
        required={true}
        autoFocus={true}
        value={state.targetUrl}
        onChange={(value) => onChange({ ...state, targetUrl: value })}
        errorMessage={urlError}
        disabled={disabled}
      />

      <EventSubscriptionsField
        value={state.eventSubscriptions}
        onChange={(next) => onChange({ ...state, eventSubscriptions: next })}
        disabled={disabled}
      />

      {!hideEventTypeScope && (
        <SelectField<string>
          name="eventTypeId"
          label={t`Scope`}
          description={t`Limit deliveries to bookings for a single service, or leave unscoped to receive everything.`}
          items={scopeItems}
          value={state.eventTypeId ?? UNSCOPED_VALUE}
          onValueChange={(value) =>
            onChange({ ...state, eventTypeId: value === null || value === UNSCOPED_VALUE ? null : value })
          }
          disabled={disabled}
        >
          <SelectTrigger>
            <SelectValue>{(value: string) => scopeItems.find((item) => item.value === value)?.label}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {scopeItems.map((item) => (
              <SelectItem key={item.value} value={item.value}>
                {item.label}
              </SelectItem>
            ))}
          </SelectContent>
        </SelectField>
      )}

      <SwitchField
        name="active"
        label={t`Active`}
        checked={state.active}
        onCheckedChange={(checked) => onChange({ ...state, active: checked })}
        disabled={disabled}
      />
      <p className="-mt-2 text-xs text-muted-foreground">
        <Trans>Paused webhooks stop receiving deliveries until reactivated.</Trans>
      </p>
    </div>
  );
}

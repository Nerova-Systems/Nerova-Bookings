/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { getEventTypeSettings, updateEventTypeSettings, updateEventTypeSettingsSection } from "../schedulingTypes";
import { DisabledFeatureRow, EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeAdvancedTab({ value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const interfaceLanguages = [
    { value: "auto", label: t`Browser default` },
    { value: "en-US", label: t`English` },
    { value: "da-DK", label: t`Danish` }
  ];
  const hasRecurrence = settings.recurrence !== null;

  const updateSettings = (updater: Parameters<typeof updateEventTypeSettings>[1]) =>
    onChange(updateEventTypeSettings(value, updater));
  const updateConfirmationPolicy = (
    confirmationPolicy: Partial<ReturnType<typeof getEventTypeSettings>["confirmationPolicy"]>
  ) => {
    onChange(
      updateEventTypeSettingsSection(value, "confirmationPolicy", (currentConfirmationPolicy) => ({
        ...currentConfirmationPolicy,
        ...confirmationPolicy
      }))
    );
  };
  const updateCancellationPolicy = (
    cancellationPolicy: Partial<ReturnType<typeof getEventTypeSettings>["cancellationPolicy"]>
  ) => {
    onChange(
      updateEventTypeSettingsSection(value, "cancellationPolicy", (currentCancellationPolicy) => ({
        ...currentCancellationPolicy,
        ...cancellationPolicy
      }))
    );
  };
  const updateReschedulePolicy = (
    reschedulePolicy: Partial<ReturnType<typeof getEventTypeSettings>["reschedulePolicy"]>
  ) => {
    onChange(
      updateEventTypeSettingsSection(value, "reschedulePolicy", (currentReschedulePolicy) => ({
        ...currentReschedulePolicy,
        ...reschedulePolicy
      }))
    );
  };
  const updateRedirects = (redirects: Partial<ReturnType<typeof getEventTypeSettings>["redirects"]>) => {
    onChange(
      updateEventTypeSettingsSection(value, "redirects", (currentRedirects) => ({
        ...currentRedirects,
        ...redirects
      }))
    );
  };
  const updateSeats = (seats: Partial<ReturnType<typeof getEventTypeSettings>["seats"]>) => {
    onChange(
      updateEventTypeSettingsSection(value, "seats", (currentSeats) => ({
        ...currentSeats,
        ...seats
      }))
    );
  };

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
          title={<Trans>Confirmation</Trans>}
          description={<Trans>Control manual approval and booker email checks.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="requiresConfirmation"
              label={t`Requires confirmation`}
              checked={settings.confirmationPolicy.requiresConfirmation}
              onCheckedChange={(requiresConfirmation) => updateConfirmationPolicy({ requiresConfirmation })}
            />
            <SwitchField
              name="requiresBookerEmailVerification"
              label={t`Email verification`}
              checked={settings.confirmationPolicy.requiresBookerEmailVerification}
              onCheckedChange={(requiresBookerEmailVerification) =>
                updateConfirmationPolicy({ requiresBookerEmailVerification })
              }
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Cancellation and reschedule</Trans>}
          description={<Trans>Set whether bookers can change bookings and how much notice is required.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="allowCancellation"
              label={t`Allow cancellation`}
              checked={settings.cancellationPolicy.allowCancellation}
              onCheckedChange={(allowCancellation) => updateCancellationPolicy({ allowCancellation })}
            />
            <NumberField
              name="cancellationMinimumNoticeMinutes"
              label={t`Cancellation notice`}
              minValue={0}
              maxValue={525600}
              allowEmpty={true}
              disabled={!settings.cancellationPolicy.allowCancellation}
              value={settings.cancellationPolicy.minimumNoticeMinutes ?? undefined}
              onChange={(minimumNoticeMinutes) => updateCancellationPolicy({ minimumNoticeMinutes })}
            />
            <SwitchField
              name="allowReschedule"
              label={t`Allow reschedule`}
              checked={settings.reschedulePolicy.allowReschedule}
              onCheckedChange={(allowReschedule) => updateReschedulePolicy({ allowReschedule })}
            />
            <NumberField
              name="rescheduleMinimumNoticeMinutes"
              label={t`Reschedule notice`}
              minValue={0}
              maxValue={525600}
              allowEmpty={true}
              disabled={!settings.reschedulePolicy.allowReschedule}
              value={settings.reschedulePolicy.minimumNoticeMinutes ?? undefined}
              onChange={(minimumNoticeMinutes) => updateReschedulePolicy({ minimumNoticeMinutes })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Redirects</Trans>}
          description={<Trans>Send bookers to a custom page after success or cancellation.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <TextField
              name="successUrl"
              label={t`Success URL`}
              value={settings.redirects.successUrl ?? ""}
              onChange={(successUrl) => updateRedirects({ successUrl: successUrl || null })}
            />
            <TextField
              name="cancellationUrl"
              label={t`Cancellation URL`}
              value={settings.redirects.cancellationUrl ?? ""}
              onChange={(cancellationUrl) => updateRedirects({ cancellationUrl: cancellationUrl || null })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Access</Trans>}
          description={<Trans>Set language and private booking links.</Trans>}
        >
          <SelectField
            name="interfaceLanguage"
            label={t`Interface language`}
            items={interfaceLanguages}
            value={settings.interfaceLanguage ?? "auto"}
            onValueChange={(interfaceLanguage) =>
              updateSettings((nextSettings) => ({
                ...nextSettings,
                interfaceLanguage: interfaceLanguage === "auto" ? null : (interfaceLanguage ?? null)
              }))
            }
          >
            <SelectTrigger>
              <SelectValue>
                {(interfaceLanguage: string) =>
                  interfaceLanguages.find((item) => item.value === interfaceLanguage)?.label
                }
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {interfaceLanguages.map((interfaceLanguage) => (
                <SelectItem key={interfaceLanguage.value} value={interfaceLanguage.value}>
                  {interfaceLanguage.label}
                </SelectItem>
              ))}
            </SelectContent>
          </SelectField>
          <TextAreaField
            name="privateLinks"
            label={t`Private links`}
            lines={3}
            value={settings.privateLinks.join("\n")}
            onChange={(privateLinksText) =>
              updateSettings((nextSettings) => ({
                ...nextSettings,
                privateLinks: parseLines(privateLinksText)
              }))
            }
          />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Seats</Trans>}
          description={
            hasRecurrence ? (
              <Trans>Seats cannot be enabled while recurring events are enabled.</Trans>
            ) : (
              <Trans>Offer multiple seats for each available time.</Trans>
            )
          }
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="seatsEnabled"
              label={t`Offer seats`}
              checked={settings.seats.enabled}
              disabled={hasRecurrence}
              onCheckedChange={(enabled) =>
                updateSeats({ enabled, capacity: enabled ? (settings.seats.capacity ?? 2) : null })
              }
            />
            <NumberField
              name="seatsCapacity"
              label={t`Seat capacity`}
              minValue={1}
              maxValue={10000}
              disabled={!settings.seats.enabled || hasRecurrence}
              value={settings.seats.capacity ?? 2}
              onChange={(capacity) => updateSeats({ capacity: capacity ?? 2 })}
            />
            <SwitchField
              name="showAttendeeInfo"
              label={t`Show attendee info`}
              checked={settings.seats.showAttendeeInfo}
              disabled={!settings.seats.enabled || hasRecurrence}
              onCheckedChange={(showAttendeeInfo) => updateSeats({ showAttendeeInfo })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Connected features</Trans>}
          description={<Trans>These features need more integration before they can be edited here.</Trans>}
        >
          <DisabledFeatureRow
            title={<Trans>Destination calendar</Trans>}
            description={<Trans>Choose where confirmed bookings should be written.</Trans>}
          />
          <DisabledFeatureRow
            title={<Trans>Workflows</Trans>}
            description={<Trans>Automate reminders and follow-up messages for this event type.</Trans>}
          />
          <DisabledFeatureRow
            title={<Trans>Webhooks</Trans>}
            description={<Trans>Notify external systems when bookings change.</Trans>}
          />
          <DisabledFeatureRow
            title={<Trans>Apps</Trans>}
            description={<Trans>Connect installed apps to this event type.</Trans>}
          />
          <DisabledFeatureRow
            title={<Trans>AI</Trans>}
            description={<Trans>AI settings are not connected to this editor yet.</Trans>}
          />
        </EventTypeTabSection>
      </div>
    </FormValidationContext.Provider>
  );
}

function parseLines(value: string) {
  return value
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean);
}

/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Checkbox } from "@repo/ui/components/Checkbox";
import { DateField } from "@repo/ui/components/DateField";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { SettingsToggle } from "@repo/ui/components/SettingsToggle";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextField } from "@repo/ui/components/TextField";
import { ArrowDownIcon, ArrowUpIcon, PlusIcon, TrashIcon } from "lucide-react";
import { useState } from "react";

import { api, type Schemas } from "@/shared/lib/api/client";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { getEventTypeSettings, updateEventTypeSettings, updateEventTypeSettingsSection } from "../schedulingTypes";
import { DisabledFeatureRow, EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeAdvancedTab({ value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const coreConnectorAccountsQuery = api.useQuery("get", "/api/connectors/core/accounts");
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
  const updateDestinationCalendar = (destinationCalendar: EventTypeSettings["destinationCalendar"]) => {
    updateSettings((nextSettings) => ({ ...nextSettings, destinationCalendar }));
  };
  const updateDefaultConferencing = (defaultConferencing: EventTypeSettings["defaultConferencing"]) => {
    updateSettings((nextSettings) => ({ ...nextSettings, defaultConferencing }));
  };
  const updateMetadata = (metadataPatch: Record<string, string>) => {
    updateSettings((nextSettings) => ({ ...nextSettings, metadata: { ...nextSettings.metadata, ...metadataPatch } }));
  };
  const metadata = settings.metadata;
  const calendarEventName =
    typeof metadata.calendarEventName === "string"
      ? metadata.calendarEventName
      : `${value.title} between {Organizer} and {Scheduler}`;
  const requiresCancellationReason =
    typeof metadata.requiresCancellationReason === "string"
      ? metadata.requiresCancellationReason
      : "mandatory-host-only";
  const canSendCalVideoTranscriptionEmails = metadata.canSendCalVideoTranscriptionEmails === "true";

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <div className="grid gap-5">
        <EventTypeTabSection title={<Trans>Calendar event</Trans>}>
          <TextField
            name="calendarEventName"
            label={t`Calendar event name`}
            value={calendarEventName}
            onChange={(nextCalendarEventName) => updateMetadata({ calendarEventName: nextCalendarEventName })}
          />
          <div className="grid gap-2">
            <div className="text-sm font-medium">
              <Trans>Add to calendar</Trans>
            </div>
            <div className="rounded-md border px-3 py-2 text-sm text-muted-foreground">
              {settings.destinationCalendar ? (
                <span>
                  <Badge variant="secondary" className="mr-2">
                    <Trans>Default</Trans>
                  </Badge>
                  {settings.destinationCalendar.externalId}
                </span>
              ) : (
                <Trans>No destination calendar selected</Trans>
              )}
            </div>
            <p className="text-sm text-muted-foreground">
              <Trans>We will display this email address as the organizer, and send confirmation emails here.</Trans>
            </p>
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Layout</Trans>}
          description={<Trans>You can select multiple and your bookers can switch views.</Trans>}
        >
          <div className="grid gap-4 lg:grid-cols-3">
            {[
              { value: "month", label: t`Month`, description: t`Default` },
              { value: "week", label: t`Weekly`, description: t`Calendar grid` },
              { value: "column", label: t`Column`, description: t`Time slots` }
            ].map((layout) => {
              const isSelected = settings.bookerLayout === layout.value;
              return (
                <Button
                  key={layout.value}
                  type="button"
                  variant="ghost"
                  aria-pressed={isSelected}
                  className="grid h-auto w-full gap-3 rounded-lg border p-3 text-left transition-colors hover:bg-muted/60 aria-pressed:border-primary max-sm:w-full"
                  onClick={() => updateSettings((nextSettings) => ({ ...nextSettings, bookerLayout: layout.value }))}
                >
                  <div className="aspect-[2/1] rounded-md border bg-muted p-3">
                    <div className="mb-3 h-6 rounded bg-background" />
                    <div className="grid grid-cols-5 gap-2">
                      {Array.from({ length: 15 }).map((_, index) => (
                        <span key={index} className="h-2 rounded bg-background" />
                      ))}
                    </div>
                  </div>
                  <span className="flex items-center gap-2 font-medium">
                    <span
                      aria-hidden="true"
                      className="flex size-4 items-center justify-center rounded-sm border bg-background text-xs"
                    >
                      {isSelected ? <span className="size-2 rounded-sm bg-primary" /> : null}
                    </span>
                    {layout.label}
                    {isSelected && <span className="text-muted-foreground">({layout.description})</span>}
                  </span>
                </Button>
              );
            })}
          </div>
          <div className="grid gap-2">
            <div className="text-sm font-medium">
              <Trans>Default view</Trans>
            </div>
            <div className="flex w-fit overflow-hidden rounded-md border">
              {[
                { value: "month", label: t`Month` },
                { value: "week", label: t`Weekly` },
                { value: "column", label: t`Column` }
              ].map((layout) => (
                <Button
                  key={layout.value}
                  type="button"
                  variant={settings.bookerLayout === layout.value ? "secondary" : "ghost"}
                  size="sm"
                  className="rounded-none border-r last:border-r-0"
                  onClick={() => updateSettings((nextSettings) => ({ ...nextSettings, bookerLayout: layout.value }))}
                >
                  {layout.label}
                </Button>
              ))}
            </div>
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Booking questions</Trans>}
          description={<Trans>Customize the questions asked on the booking page. Learn more</Trans>}
        >
          <BookingQuestionsPreview fields={settings.bookingFields} />
        </EventTypeTabSection>
        <EventTypeTabSection title={<Trans>Require cancellation reason</Trans>}>
          <SelectField
            name="requiresCancellationReason"
            label={t`Require cancellation reason`}
            items={[
              { value: "mandatory-host-only", label: t`Mandatory for host only` },
              { value: "mandatory-both", label: t`Mandatory for both` },
              { value: "mandatory-attendee-only", label: t`Mandatory for attendee only` },
              { value: "optional-both", label: t`Optional for both` }
            ]}
            value={requiresCancellationReason}
            onValueChange={(nextValue) =>
              updateMetadata({ requiresCancellationReason: nextValue ?? "mandatory-host-only" })
            }
          >
            <SelectTrigger>
              <SelectValue>
                {(nextValue: string) =>
                  ({
                    "mandatory-host-only": t`Mandatory for host only`,
                    "mandatory-both": t`Mandatory for both`,
                    "mandatory-attendee-only": t`Mandatory for attendee only`,
                    "optional-both": t`Optional for both`
                  })[nextValue] ?? nextValue
                }
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="mandatory-host-only">{t`Mandatory for host only`}</SelectItem>
              <SelectItem value="mandatory-both">{t`Mandatory for both`}</SelectItem>
              <SelectItem value="mandatory-attendee-only">{t`Mandatory for attendee only`}</SelectItem>
              <SelectItem value="optional-both">{t`Optional for both`}</SelectItem>
            </SelectContent>
          </SelectField>
        </EventTypeTabSection>
        <SettingsToggle
          title={<Trans>Requires confirmation</Trans>}
          description={
            <Trans>
              The booking needs to be manually confirmed before it is pushed to your calendar and a confirmation is
              sent. Learn more
            </Trans>
          }
          checked={settings.confirmationPolicy.requiresConfirmation}
          onCheckedChange={(requiresConfirmation) => updateConfirmationPolicy({ requiresConfirmation })}
        />
        <SettingsToggle
          title={<Trans>Disable cancelling</Trans>}
          description={<Trans>Disable event cancellation via calendar invite or email. Learn more</Trans>}
          checked={!settings.cancellationPolicy.allowCancellation}
          onCheckedChange={(disabledCancelling) => updateCancellationPolicy({ allowCancellation: !disabledCancelling })}
        />
        <SettingsToggle
          title={<Trans>Disable rescheduling</Trans>}
          description={<Trans>Disable rescheduling via calendar invite or email. Learn more</Trans>}
          checked={!settings.reschedulePolicy.allowReschedule}
          onCheckedChange={(disableRescheduling) => updateReschedulePolicy({ allowReschedule: !disableRescheduling })}
        />
        <SettingsToggle
          title={<Trans>Send Cal Video transcription emails</Trans>}
          description={
            <Trans>
              Send emails with the transcription of the Cal Video after the meeting ends. Requires a paid plan.
            </Trans>
          }
          checked={canSendCalVideoTranscriptionEmails}
          onCheckedChange={(nextValue) => updateMetadata({ canSendCalVideoTranscriptionEmails: String(nextValue) })}
        />
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
          <PrivateLinksEditor
            value={settings.privateLinks}
            onChange={(privateLinks) => updateSettings((nextSettings) => ({ ...nextSettings, privateLinks }))}
          />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Booking fields</Trans>}
          description={<Trans>Collect Cal.com-compatible custom answers from bookers.</Trans>}
        >
          <BookingFieldsEditor
            value={settings.bookingFields}
            onChange={(bookingFields) => updateSettings((nextSettings) => ({ ...nextSettings, bookingFields }))}
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
          description={<Trans>Choose the core calendar and conferencing apps used by this event type.</Trans>}
        >
          <CoreConnectorSettings
            settings={settings}
            accounts={coreConnectorAccountsQuery.data?.accounts ?? []}
            integrations={coreConnectorAccountsQuery.data?.integrations ?? []}
            isLoading={coreConnectorAccountsQuery.isLoading}
            onAccountsChanged={() => void coreConnectorAccountsQuery.refetch()}
            onSelectedCalendarsChange={(selectedCalendars) =>
              updateSettings((nextSettings) => ({ ...nextSettings, selectedCalendars }))
            }
            onDestinationCalendarChange={updateDestinationCalendar}
            onDefaultConferencingChange={updateDefaultConferencing}
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

type EventTypeSettings = ReturnType<typeof getEventTypeSettings>;
type CoreConnectorAccount = Schemas["CoreConnectorAccountResponse"];
type CoreConnectorIntegration = Schemas["CoreConnectorIntegrationResponse"];
type BookingField = EventTypeSettings["bookingFields"][number];
type BookingFieldOption = BookingField["options"][number];
type PrivateLink = EventTypeSettings["privateLinks"][number];

function BookingQuestionsPreview({ fields }: Readonly<{ fields: BookingField[] }>) {
  const defaultFields = [
    { label: t`Your name`, type: t`Name`, state: t`Required`, enabled: true },
    { label: t`Email address`, type: t`Email`, state: t`Required`, enabled: true },
    { label: t`Phone number`, type: t`Phone`, state: t`Hidden`, enabled: false },
    { label: t`What is this meeting about?`, type: t`Short Text`, state: t`Hidden`, enabled: false },
    { label: t`Additional notes`, type: t`Long Text`, state: t`Optional`, enabled: true },
    { label: t`Add guests`, type: t`Multiple Emails`, state: t`Optional`, enabled: true },
    { label: t`Reason for reschedule`, type: t`Long Text`, state: t`Optional`, enabled: true }
  ];
  const fieldRows =
    fields.length > 0
      ? fields.map((field) => ({
          label: field.label || field.name,
          type: field.type,
          state: field.hidden ? t`Hidden` : field.required ? t`Required` : t`Optional`,
          enabled: !field.hidden
        }))
      : defaultFields;

  return (
    <div className="overflow-hidden rounded-lg border">
      <div className="flex items-start justify-between gap-3 border-b p-4">
        <div>
          <div className="font-semibold">
            <Trans>Confirmation</Trans>
          </div>
          <p className="text-sm text-muted-foreground">
            <Trans>What your booker should provide to receive confirmations</Trans>
          </p>
        </div>
        <div className="flex overflow-hidden rounded-md border text-sm">
          <span className="bg-muted px-3 py-2">
            <Trans>Email</Trans>
          </span>
          <span className="px-3 py-2 text-muted-foreground">
            <Trans>Phone</Trans>
          </span>
        </div>
      </div>
      <div>
        {fieldRows.map((field) => (
          <div
            key={`${field.label}-${field.type}`}
            className="flex items-center justify-between gap-4 border-b p-4 last:border-b-0"
          >
            <div className="min-w-0">
              <div className="flex min-w-0 flex-wrap items-center gap-2 font-medium">
                <span className="truncate">{field.label}</span>
                <Badge variant="secondary">{field.state}</Badge>
              </div>
              <div className="text-sm text-muted-foreground">{field.type}</div>
            </div>
            <div className="flex items-center gap-3">
              <Checkbox checked={field.enabled} disabled aria-label={t`Field enabled`} />
              <Button type="button" variant="outline" size="sm">
                <Trans>Edit</Trans>
              </Button>
            </div>
          </div>
        ))}
      </div>
      <div className="border-t p-4">
        <Button type="button" variant="ghost">
          <PlusIcon />
          <Trans>Add a question</Trans>
        </Button>
      </div>
    </div>
  );
}

function CoreConnectorSettings({
  settings,
  accounts,
  integrations,
  isLoading,
  onAccountsChanged,
  onSelectedCalendarsChange,
  onDestinationCalendarChange,
  onDefaultConferencingChange
}: Readonly<{
  settings: EventTypeSettings;
  accounts: CoreConnectorAccount[];
  integrations: CoreConnectorIntegration[];
  isLoading: boolean;
  onAccountsChanged: () => void;
  onSelectedCalendarsChange: (selectedCalendars: EventTypeSettings["selectedCalendars"]) => void;
  onDestinationCalendarChange: (destinationCalendar: EventTypeSettings["destinationCalendar"]) => void;
  onDefaultConferencingChange: (defaultConferencing: EventTypeSettings["defaultConferencing"]) => void;
}>) {
  const calendarAccounts = accounts.filter((account) => isCalendarIntegration(account.integration));
  const destinationCalendarOptions = [
    { value: "none", label: t`No destination calendar` },
    ...calendarAccounts.flatMap((account) =>
      account.calendars.map((calendar) => ({
        value: connectorCalendarValue(account.integration, calendar.externalId, account.id),
        label: `${calendar.name} (${connectorLabel(account.integration)} - ${account.accountEmail})`
      }))
    )
  ];
  const conferencingOptions = [
    { value: "none", label: t`No default conferencing` },
    ...accounts.flatMap((account) => conferencingAppsForAccount(account))
  ];
  const destinationCalendar = settings.destinationCalendar;
  const defaultConferencing = settings.defaultConferencing;
  const destinationValue = destinationCalendar
    ? connectorCalendarValue(
        destinationCalendar.integration,
        destinationCalendar.externalId,
        destinationCalendar.credentialId
      )
    : "none";
  const conferencingValue = defaultConferencing
    ? connectorConferencingValue(defaultConferencing.app, defaultConferencing.credentialId)
    : "none";

  return (
    <div className="grid gap-5">
      {isLoading && (
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>Loading connected accounts...</Trans>
        </div>
      )}
      <CoreConnectorConnectionControls
        accounts={accounts}
        integrations={integrations}
        onAccountsChanged={onAccountsChanged}
      />
      <div className="grid gap-4 rounded-md border p-4">
        <SelectField
          name="destinationCalendarIntegration"
          label={t`Destination calendar`}
          items={destinationCalendarOptions}
          value={destinationValue}
          onValueChange={(nextValue) =>
            !nextValue || nextValue === "none"
              ? onDestinationCalendarChange(null)
              : onDestinationCalendarChange(parseConnectorCalendarValue(nextValue))
          }
        >
          <SelectTrigger>
            <SelectValue>
              {(nextValue: string) =>
                destinationCalendarOptions.find((item) => item.value === nextValue)?.label ?? nextValue
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {destinationCalendarOptions.map((option) => (
              <SelectItem key={option.value} value={option.value}>
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </SelectField>
      </div>
      <div className="grid gap-4 rounded-md border p-4">
        <SelectField
          name="defaultConferencingApp"
          label={t`Default conferencing`}
          items={conferencingOptions}
          value={conferencingValue}
          onValueChange={(nextValue) =>
            !nextValue || nextValue === "none"
              ? onDefaultConferencingChange(null)
              : onDefaultConferencingChange(parseConnectorConferencingValue(nextValue))
          }
        >
          <SelectTrigger>
            <SelectValue>
              {(nextValue: string) => conferencingOptions.find((item) => item.value === nextValue)?.label ?? nextValue}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {conferencingOptions.map((option) => (
              <SelectItem key={option.value} value={option.value}>
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </SelectField>
      </div>
      <div className="grid gap-3 rounded-md border p-4">
        <div className="text-sm font-medium">
          <Trans>Selected calendars</Trans>
        </div>
        {calendarAccounts.length === 0 ? (
          <div className="text-sm text-muted-foreground">
            <Trans>No connected Google or Microsoft calendars.</Trans>
          </div>
        ) : (
          <div className="grid gap-2">
            {calendarAccounts.flatMap((account) =>
              account.calendars.map((calendar) => {
                const checkboxId = `selected-calendar-${account.id}-${calendar.externalId}`;
                const checked = settings.selectedCalendars.some(
                  (selectedCalendar) =>
                    selectedCalendar.integration === account.integration &&
                    selectedCalendar.externalId === calendar.externalId &&
                    selectedCalendar.credentialId === account.id
                );
                return (
                  <label
                    key={`${account.id}-${calendar.externalId}`}
                    htmlFor={checkboxId}
                    className="flex items-start gap-3 rounded-md border p-3 text-sm"
                  >
                    <Checkbox
                      id={checkboxId}
                      checked={checked}
                      onCheckedChange={(nextChecked) =>
                        onSelectedCalendarsChange(
                          nextChecked
                            ? [
                                ...settings.selectedCalendars,
                                {
                                  integration: account.integration,
                                  externalId: calendar.externalId,
                                  credentialId: account.id
                                }
                              ]
                            : settings.selectedCalendars.filter(
                                (selectedCalendar) =>
                                  selectedCalendar.integration !== account.integration ||
                                  selectedCalendar.externalId !== calendar.externalId ||
                                  selectedCalendar.credentialId !== account.id
                              )
                        )
                      }
                    />
                    <span className="min-w-0">
                      <span className="block font-medium">{calendar.name}</span>
                      <span className="block text-muted-foreground">
                        {connectorLabel(account.integration)} - {account.accountEmail}
                      </span>
                    </span>
                  </label>
                );
              })
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function CoreConnectorConnectionControls({
  accounts,
  integrations,
  onAccountsChanged
}: Readonly<{
  accounts: CoreConnectorAccount[];
  integrations: CoreConnectorIntegration[];
  onAccountsChanged: () => void;
}>) {
  const [pendingIntegration, setPendingIntegration] = useState<string | null>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const coreIntegrations = [
    { integration: "google-calendar", label: t`Google Calendar`, connectLabel: t`Connect Google` },
    { integration: "office365-calendar", label: t`Office 365 Calendar`, connectLabel: t`Connect Microsoft` },
    { integration: "zoom-video", label: "Zoom", connectLabel: t`Connect Zoom` }
  ].map((integration) => ({
    ...integration,
    configured:
      integrations.find((configuredIntegration) => configuredIntegration.integration === integration.integration)
        ?.configured ?? false
  }));

  const startOAuth = async (integration: string) => {
    setError(null);
    setPendingIntegration(integration);
    try {
      const response = await fetch(
        `${import.meta.env.PUBLIC_URL}/api/connectors/core/${integration}/authorization-url?returnTo=${encodeURIComponent(
          window.location.pathname
        )}`,
        { credentials: "include" }
      );
      if (!response.ok) throw new Error(t`Connector is not configured.`);
      const authorization = (await response.json()) as { url?: string };
      if (!authorization.url) throw new Error(t`Connector authorization URL was not returned.`);
      window.location.href = authorization.url;
    } catch (unknownError) {
      setError(unknownError instanceof Error ? unknownError.message : t`Could not start connector setup.`);
      setPendingIntegration(null);
    }
  };

  const deleteAccount = async (credentialId: string) => {
    setError(null);
    setPendingDeleteId(credentialId);
    try {
      const response = await fetch(
        `${import.meta.env.PUBLIC_URL}/api/connectors/core/accounts/${encodeURIComponent(credentialId)}`,
        {
          method: "DELETE",
          credentials: "include",
          headers: { "x-xsrf-token": import.meta.antiforgeryToken }
        }
      );
      if (!response.ok) throw new Error(t`Could not disconnect connector account.`);
      onAccountsChanged();
    } catch (unknownError) {
      setError(unknownError instanceof Error ? unknownError.message : t`Could not disconnect connector account.`);
    } finally {
      setPendingDeleteId(null);
    }
  };

  return (
    <div className="grid gap-4 rounded-md border p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="text-sm font-medium">
            <Trans>Connector accounts</Trans>
          </div>
          <div className="text-sm text-muted-foreground">
            <Trans>Connect Google, Microsoft, and Zoom accounts for calendars and conferencing.</Trans>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {coreIntegrations.map((integration) => (
            <Button
              key={integration.integration}
              type="button"
              variant="outline"
              size="sm"
              disabled={!integration.configured || pendingIntegration !== null || pendingDeleteId !== null}
              title={integration.configured ? undefined : t`Configuration required`}
              onClick={() => void startOAuth(integration.integration)}
            >
              <PlusIcon />
              {pendingIntegration === integration.integration
                ? t`Connecting...`
                : integration.configured
                  ? integration.connectLabel
                  : t`Configuration required`}
            </Button>
          ))}
        </div>
      </div>
      {error && <div className="rounded-md border border-destructive/40 p-3 text-sm text-destructive">{error}</div>}
      {accounts.length === 0 ? (
        <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          <Trans>No connector accounts are connected yet.</Trans>
        </div>
      ) : (
        <div className="grid gap-2">
          {accounts.map((account) => (
            <div key={account.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3">
              <div className="min-w-0">
                <div className="text-sm font-medium">{connectorLabel(account.integration)}</div>
                <div className="truncate text-sm text-muted-foreground">
                  {account.displayName} - {account.accountEmail}
                </div>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={pendingDeleteId !== null || pendingIntegration !== null}
                onClick={() => void deleteAccount(account.id)}
              >
                <TrashIcon />
                {pendingDeleteId === account.id ? t`Disconnecting...` : t`Disconnect`}
              </Button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function isCalendarIntegration(integration: string) {
  return integration === "google-calendar" || integration === "office365-calendar";
}

function connectorLabel(integration: string) {
  if (integration === "google-calendar") return t`Google Calendar`;
  if (integration === "office365-calendar") return t`Office 365 Calendar`;
  if (integration === "zoom-video") return "Zoom";
  return integration;
}

function connectorCalendarValue(integration: string, externalId: string, credentialId?: string | null) {
  return `${integration}|${externalId}|${credentialId ?? ""}`;
}

function parseConnectorCalendarValue(value: string): NonNullable<EventTypeSettings["destinationCalendar"]> {
  const [integration, externalId, ...credentialIdParts] = value.split("|");
  const credentialId = credentialIdParts.join("|");
  return { integration, externalId, credentialId: credentialId || null };
}

function connectorConferencingValue(app: string, credentialId?: string | null) {
  return `${app}|${credentialId ?? ""}`;
}

function parseConnectorConferencingValue(value: string): NonNullable<EventTypeSettings["defaultConferencing"]> {
  const [app, credentialId] = value.split("|");
  return { app, credentialId: credentialId || null };
}

function conferencingAppsForAccount(account: CoreConnectorAccount) {
  if (account.integration === "google-calendar") {
    return [
      {
        value: connectorConferencingValue("google-meet", account.id),
        label: `${t`Google Meet`} (${account.accountEmail})`
      }
    ];
  }

  if (account.integration === "office365-calendar") {
    return [
      {
        value: connectorConferencingValue("office365-video", account.id),
        label: `${t`Office 365 video`} (${account.accountEmail})`
      }
    ];
  }

  if (account.integration === "zoom-video") {
    return [
      {
        value: connectorConferencingValue("zoom-video", account.id),
        label: `Zoom (${account.accountEmail})`
      }
    ];
  }

  return [];
}

function BookingFieldsEditor({
  value,
  onChange
}: Readonly<{ value: BookingField[]; onChange: (value: BookingField[]) => void }>) {
  const bookingFieldTypes = getBookingFieldTypes();
  const updateField = (index: number, patch: Partial<BookingField>) => {
    onChange(value.map((field, fieldIndex) => (fieldIndex === index ? { ...field, ...patch } : field)));
  };

  const moveField = (index: number, offset: -1 | 1) => {
    const targetIndex = index + offset;
    if (targetIndex < 0 || targetIndex >= value.length) return;
    const nextValue = [...value];
    const [field] = nextValue.splice(index, 1);
    nextValue.splice(targetIndex, 0, field);
    onChange(nextValue);
  };

  const removeField = (index: number) => {
    onChange(value.filter((_, fieldIndex) => fieldIndex !== index));
  };

  return (
    <div className="grid gap-3">
      {value.length === 0 && (
        <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          <Trans>No custom booking fields yet.</Trans>
        </div>
      )}
      {value.map((field, index) => (
        <div key={`${field.name}-${index}`} className="grid gap-4 rounded-md border p-4">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div className="min-w-0">
              <div className="font-medium">{field.label || field.name || t`Untitled field`}</div>
              <div className="text-sm text-muted-foreground">{field.type}</div>
            </div>
            <div className="flex gap-1">
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                disabled={index === 0}
                aria-label={t`Move field up`}
                onClick={() => moveField(index, -1)}
              >
                <ArrowUpIcon />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                disabled={index === value.length - 1}
                aria-label={t`Move field down`}
                onClick={() => moveField(index, 1)}
              >
                <ArrowDownIcon />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label={t`Remove field`}
                onClick={() => removeField(index)}
              >
                <TrashIcon />
              </Button>
            </div>
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            <TextField
              name={`bookingField-${index}-label`}
              label={t`Label`}
              value={field.label}
              onChange={(label) => updateField(index, { label })}
            />
            <TextField
              name={`bookingField-${index}-name`}
              label={t`Name`}
              value={field.name}
              onChange={(name) => updateField(index, { name: slugifyFieldName(name) })}
            />
            <SelectField
              name={`bookingField-${index}-type`}
              label={t`Type`}
              items={bookingFieldTypes}
              value={field.type}
              onValueChange={(type) => updateField(index, { type: type ?? "text" })}
            >
              <SelectTrigger>
                <SelectValue>
                  {(type: string) => bookingFieldTypes.find((item) => item.value === type)?.label}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {bookingFieldTypes.map((type) => (
                  <SelectItem key={type.value} value={type.value}>
                    {type.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </SelectField>
            <TextField
              name={`bookingField-${index}-placeholder`}
              label={t`Placeholder`}
              value={field.placeholder ?? ""}
              onChange={(placeholder) => updateField(index, { placeholder: placeholder || null })}
            />
            <SwitchField
              name={`bookingField-${index}-required`}
              label={t`Required`}
              checked={field.required}
              onCheckedChange={(required) => updateField(index, { required })}
            />
            <SwitchField
              name={`bookingField-${index}-hidden`}
              label={t`Hidden`}
              checked={field.hidden}
              onCheckedChange={(hidden) => updateField(index, { hidden })}
            />
            <NumberField
              name={`bookingField-${index}-minLength`}
              label={t`Minimum length`}
              minValue={0}
              maxValue={1000}
              allowEmpty={true}
              value={field.minLength ?? undefined}
              onChange={(minLength) => updateField(index, { minLength })}
            />
            <NumberField
              name={`bookingField-${index}-maxLength`}
              label={t`Maximum length`}
              minValue={0}
              maxValue={1000}
              allowEmpty={true}
              value={field.maxLength ?? undefined}
              onChange={(maxLength) => updateField(index, { maxLength })}
            />
            <TextField
              name={`bookingField-${index}-excludeEmails`}
              label={t`Exclude emails`}
              value={field.excludeEmails ?? ""}
              onChange={(excludeEmails) => updateField(index, { excludeEmails: excludeEmails || null })}
            />
            <TextField
              name={`bookingField-${index}-requireEmails`}
              label={t`Require emails`}
              value={field.requireEmails ?? ""}
              onChange={(requireEmails) => updateField(index, { requireEmails: requireEmails || null })}
            />
            <SwitchField
              name={`bookingField-${index}-disableOnPrefill`}
              label={t`Disable on prefill`}
              checked={field.disableOnPrefill}
              onCheckedChange={(disableOnPrefill) => updateField(index, { disableOnPrefill })}
            />
          </div>
          {fieldTypeUsesOptions(field.type) && (
            <BookingFieldOptionsEditor
              fieldIndex={index}
              value={field.options ?? []}
              onChange={(options) => updateField(index, { options })}
            />
          )}
        </div>
      ))}
      <Button type="button" variant="outline" onClick={() => onChange([...value, newBookingField(value.length)])}>
        <PlusIcon />
        <Trans>Add booking field</Trans>
      </Button>
    </div>
  );
}

function BookingFieldOptionsEditor({
  fieldIndex,
  value,
  onChange
}: Readonly<{ fieldIndex: number; value: BookingFieldOption[]; onChange: (value: BookingFieldOption[]) => void }>) {
  const updateOption = (index: number, patch: Partial<BookingFieldOption>) => {
    onChange(value.map((option, optionIndex) => (optionIndex === index ? { ...option, ...patch } : option)));
  };

  return (
    <div className="grid gap-3 rounded-md bg-muted/40 p-3">
      <div className="text-sm font-medium">
        <Trans>Options</Trans>
      </div>
      {value.map((option, index) => (
        <div key={`${option.value}-${index}`} className="grid gap-3 md:grid-cols-[1fr_1fr_8rem_auto]">
          <TextField
            name={`bookingField-${fieldIndex}-option-${index}-label`}
            label={t`Option label`}
            value={option.label}
            onChange={(label) => updateOption(index, { label })}
          />
          <TextField
            name={`bookingField-${fieldIndex}-option-${index}-value`}
            label={t`Option value`}
            value={option.value}
            onChange={(optionValue) => updateOption(index, { value: optionValue })}
          />
          <NumberField
            name={`bookingField-${fieldIndex}-option-${index}-price`}
            label={t`Price`}
            minValue={0}
            allowEmpty={true}
            value={option.price ?? undefined}
            onChange={(price) => updateOption(index, { price: price ?? null })}
          />
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            className="self-end"
            aria-label={t`Remove option`}
            onClick={() => onChange(value.filter((_, optionIndex) => optionIndex !== index))}
          >
            <TrashIcon />
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" onClick={() => onChange([...value, newBookingFieldOption()])}>
        <PlusIcon />
        <Trans>Add option</Trans>
      </Button>
    </div>
  );
}

function PrivateLinksEditor({
  value,
  onChange
}: Readonly<{ value: PrivateLink[]; onChange: (value: PrivateLink[]) => void }>) {
  const updatePrivateLink = (index: number, patch: Partial<PrivateLink>) => {
    onChange(value.map((privateLink, linkIndex) => (linkIndex === index ? { ...privateLink, ...patch } : privateLink)));
  };

  return (
    <div className="grid gap-3">
      {value.length === 0 && (
        <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          <Trans>No private links yet.</Trans>
        </div>
      )}
      {value.map((privateLink, index) => (
        <div key={`${privateLink.link}-${index}`} className="grid gap-4 rounded-md border p-4">
          <div className="grid gap-4 md:grid-cols-[1fr_12rem_10rem_8rem_auto]">
            <TextField
              name={`privateLink-${index}-link`}
              label={t`Private link`}
              value={privateLink.link}
              onChange={(link) => updatePrivateLink(index, { link: slugifyPrivateLink(link) })}
            />
            <DateField
              name={`privateLink-${index}-expiresAt`}
              label={t`Expires`}
              value={toDateInput(privateLink.expiresAt)}
              onChange={(expiresAt) => updatePrivateLink(index, { expiresAt: fromDateInput(expiresAt) })}
            />
            <NumberField
              name={`privateLink-${index}-maxUsageCount`}
              label={t`Max uses`}
              minValue={1}
              maxValue={100000}
              allowEmpty={true}
              value={privateLink.maxUsageCount ?? undefined}
              onChange={(maxUsageCount) => updatePrivateLink(index, { maxUsageCount })}
            />
            <NumberField
              name={`privateLink-${index}-usageCount`}
              label={t`Used`}
              minValue={0}
              disabled={true}
              value={privateLink.usageCount}
              onChange={() => undefined}
            />
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              className="self-end"
              aria-label={t`Remove private link`}
              onClick={() => onChange(value.filter((_, linkIndex) => linkIndex !== index))}
            >
              <TrashIcon />
            </Button>
          </div>
        </div>
      ))}
      <Button type="button" variant="outline" onClick={() => onChange([...value, newPrivateLink()])}>
        <PlusIcon />
        <Trans>Add private link</Trans>
      </Button>
    </div>
  );
}

function newBookingField(index: number): BookingField {
  return {
    name: `question${index + 1}`,
    label: t`Question`,
    type: "text",
    required: false,
    options: [],
    placeholder: null,
    labelAsSafeHtml: null,
    defaultLabel: null,
    defaultPlaceholder: null,
    minLength: null,
    maxLength: null,
    excludeEmails: null,
    requireEmails: null,
    price: null,
    getOptionsAt: null,
    optionsInputs: {},
    variant: null,
    variantsConfig: null,
    views: [],
    hideWhenJustOneOption: false,
    hidden: false,
    editable: "user",
    sources: [],
    disableOnPrefill: false
  };
}

function newBookingFieldOption(): BookingFieldOption {
  return { label: t`Option`, value: "option", price: null };
}

function newPrivateLink(): PrivateLink {
  return { link: generatePrivateLink(), expiresAt: null, maxUsageCount: 1, usageCount: 0 };
}

function fieldTypeUsesOptions(type: string) {
  return type === "select" || type === "radio" || type === "checkbox" || type === "multiselect";
}

function getBookingFieldTypes() {
  return [
    { value: "text", label: t`Text` },
    { value: "textarea", label: t`Textarea` },
    { value: "select", label: t`Select` },
    { value: "radio", label: t`Radio` },
    { value: "checkbox", label: t`Checkbox` },
    { value: "multiselect", label: t`Multi-select` },
    { value: "boolean", label: t`Boolean` },
    { value: "email", label: t`Email` },
    { value: "multiemail", label: t`Multiple emails` },
    { value: "phone", label: t`Phone` },
    { value: "url", label: t`URL` },
    { value: "number", label: t`Number` }
  ];
}

function slugifyFieldName(value: string) {
  return value
    .trim()
    .replace(/[^a-zA-Z0-9_-]+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function slugifyPrivateLink(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9_-]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function generatePrivateLink() {
  const randomPart = globalThis.crypto?.randomUUID?.().slice(0, 8) ?? Math.random().toString(36).slice(2, 10);
  return `link-${randomPart}`;
}

function toDateInput(value: string | null) {
  return value ? value.slice(0, 10) : "";
}

function fromDateInput(value: string) {
  return value ? `${value}T23:59:59.999Z` : null;
}

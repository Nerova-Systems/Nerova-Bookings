/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { DateField } from "@repo/ui/components/DateField";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextField } from "@repo/ui/components/TextField";
import { ArrowDownIcon, ArrowUpIcon, PlusIcon, TrashIcon } from "lucide-react";

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
  const updateDestinationCalendar = (destinationCalendar: EventTypeSettings["destinationCalendar"]) => {
    updateSettings((nextSettings) => ({ ...nextSettings, destinationCalendar }));
  };
  const updateDefaultConferencing = (defaultConferencing: EventTypeSettings["defaultConferencing"]) => {
    updateSettings((nextSettings) => ({ ...nextSettings, defaultConferencing }));
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
type BookingField = EventTypeSettings["bookingFields"][number];
type BookingFieldOption = BookingField["options"][number];
type PrivateLink = EventTypeSettings["privateLinks"][number];

function CoreConnectorSettings({
  settings,
  onDestinationCalendarChange,
  onDefaultConferencingChange
}: Readonly<{
  settings: EventTypeSettings;
  onDestinationCalendarChange: (destinationCalendar: EventTypeSettings["destinationCalendar"]) => void;
  onDefaultConferencingChange: (defaultConferencing: EventTypeSettings["defaultConferencing"]) => void;
}>) {
  const calendarIntegrations = [
    { value: "none", label: t`No destination calendar` },
    { value: "google-calendar", label: t`Google Calendar` },
    { value: "office365-calendar", label: t`Office 365 Calendar` }
  ];
  const conferencingApps = [
    { value: "none", label: t`No default conferencing` },
    { value: "google-meet", label: t`Google Meet` },
    { value: "office365-video", label: t`Office 365 video` },
    { value: "zoom-video", label: t`Zoom` }
  ];
  const destinationCalendar = settings.destinationCalendar;
  const defaultConferencing = settings.defaultConferencing;

  const updateDestination = (patch: Partial<NonNullable<EventTypeSettings["destinationCalendar"]>>) => {
    const nextDestination = {
      integration: destinationCalendar?.integration || "google-calendar",
      externalId: destinationCalendar?.externalId || "primary",
      credentialId: destinationCalendar?.credentialId ?? null,
      ...patch
    };
    onDestinationCalendarChange(nextDestination);
  };
  const updateConferencing = (patch: Partial<NonNullable<EventTypeSettings["defaultConferencing"]>>) => {
    const nextConferencing = {
      app: defaultConferencing?.app || "zoom-video",
      credentialId: defaultConferencing?.credentialId ?? null,
      ...patch
    };
    onDefaultConferencingChange(nextConferencing);
  };

  return (
    <div className="grid gap-5">
      <div className="grid gap-4 rounded-md border p-4">
        <SelectField
          name="destinationCalendarIntegration"
          label={t`Destination calendar`}
          items={calendarIntegrations}
          value={destinationCalendar?.integration ?? "none"}
          onValueChange={(integration) =>
            !integration || integration === "none" ? onDestinationCalendarChange(null) : updateDestination({ integration })
          }
        >
          <SelectTrigger>
            <SelectValue>
              {(integration: string) =>
                calendarIntegrations.find((item) => item.value === integration)?.label ?? integration
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {calendarIntegrations.map((integration) => (
              <SelectItem key={integration.value} value={integration.value}>
                {integration.label}
              </SelectItem>
            ))}
          </SelectContent>
        </SelectField>
        {destinationCalendar && (
          <div className="grid gap-4 md:grid-cols-2">
            <TextField
              name="destinationCalendarExternalId"
              label={t`Calendar ID`}
              value={destinationCalendar.externalId}
              onChange={(externalId) => updateDestination({ externalId })}
            />
            <TextField
              name="destinationCalendarCredentialId"
              label={t`Credential reference`}
              value={destinationCalendar.credentialId ?? ""}
              onChange={(credentialId) => updateDestination({ credentialId: credentialId || null })}
            />
          </div>
        )}
      </div>
      <div className="grid gap-4 rounded-md border p-4">
        <SelectField
          name="defaultConferencingApp"
          label={t`Default conferencing`}
          items={conferencingApps}
          value={defaultConferencing?.app ?? "none"}
          onValueChange={(app) => (!app || app === "none" ? onDefaultConferencingChange(null) : updateConferencing({ app }))}
        >
          <SelectTrigger>
            <SelectValue>
              {(app: string) => conferencingApps.find((item) => item.value === app)?.label ?? app}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {conferencingApps.map((app) => (
              <SelectItem key={app.value} value={app.value}>
                {app.label}
              </SelectItem>
            ))}
          </SelectContent>
        </SelectField>
        {defaultConferencing && (
          <TextField
            name="defaultConferencingCredentialId"
            label={t`Credential reference`}
            value={defaultConferencing.credentialId ?? ""}
            onChange={(credentialId) => updateConferencing({ credentialId: credentialId || null })}
          />
        )}
      </div>
      <div className="grid gap-3 rounded-md border p-4">
        <div className="text-sm font-medium">
          <Trans>Selected calendars</Trans>
        </div>
        {settings.selectedCalendars.length === 0 ? (
          <div className="text-sm text-muted-foreground">
            <Trans>No selected calendars. Availability only uses Nerova bookings.</Trans>
          </div>
        ) : (
          <div className="grid gap-2 text-sm">
            {settings.selectedCalendars.map((calendar) => (
              <div key={`${calendar.integration}-${calendar.externalId}-${calendar.credentialId ?? ""}`} className="rounded-md bg-muted/40 p-3">
                <div className="font-medium">{calendar.integration}</div>
                <div className="text-muted-foreground">{calendar.externalId}</div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
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

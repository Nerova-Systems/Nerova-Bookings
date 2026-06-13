/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@repo/ui/components/Collapsible";
import { Form, FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { ChevronDownIcon } from "lucide-react";
import { useEffect, useState } from "react";

import type { ApiValidationError, EventTypePayload, EventTypeSettings, Schedule } from "../schedulingTypes";
import type { EventTypeTabName } from "./eventTypeShellTypes";

import { EventTypeAdvancedTab } from "../event-type-tabs/EventTypeAdvancedTab";
import { EventTypeAppsTab } from "../event-type-tabs/EventTypeAppsTab";
import { EventTypeAvailabilityTab } from "../event-type-tabs/EventTypeAvailabilityTab";
import { EventTypeLimitsTab } from "../event-type-tabs/EventTypeLimitsTab";
import { EventTypeRecurringTab } from "../event-type-tabs/EventTypeRecurringTab";
import { EventTypeImageUpload } from "../event-type-tabs/EventTypeSetupTab";
import { EventTypeTabSection } from "../event-type-tabs/EventTypeTabSection";
import { type EventTypeTabProps } from "../event-type-tabs/EventTypeTabTypes";
import { EventTypeTeamTab } from "../event-type-tabs/EventTypeTeamTab";
import { EventTypeWebhooksTab } from "../event-type-tabs/EventTypeWebhooksTab";
import { LocationTypeSelect } from "../LocationTypeSelect";
import {
  getEventTypeSettings,
  slugify,
  updateEventTypeSettings,
  updateEventTypeSettingsSection
} from "../schedulingTypes";

export const eventTypeFormId = "event-type-editor-form";

type EventTypeEditorTabsProps = Readonly<{
  eventTypeId: string;
  imageUrl: string | null;
  tabName: EventTypeTabName;
  draft: EventTypePayload;
  schedules: Schedule[];
  canSave: boolean;
  error?: ApiValidationError;
  onChange: (value: EventTypePayload) => void;
  onSubmit: () => void;
}>;

export function EventTypeEditorTabs({
  eventTypeId,
  imageUrl,
  draft,
  schedules,
  canSave,
  error,
  onChange,
  onSubmit
}: EventTypeEditorTabsProps) {
  const tabProps = { eventTypeId, imageUrl, value: draft, schedules, onChange, error };

  return (
    <Form
      id={eventTypeFormId}
      validationBehavior="aria"
      validationErrors={error?.errors}
      className="grid gap-5"
      onSubmit={(event) => {
        event.preventDefault();
        if (canSave) onSubmit();
      }}
    >
      <ServicePrimaryFields
        eventTypeId={eventTypeId}
        imageUrl={imageUrl}
        value={draft}
        onChange={onChange}
        error={error}
      />
      <AdvancedServiceSettings tabProps={tabProps} />
    </Form>
  );
}

function ServicePrimaryFields({
  eventTypeId,
  imageUrl,
  value,
  onChange,
  error
}: Readonly<Pick<EventTypeTabProps, "eventTypeId" | "imageUrl" | "value" | "onChange" | "error">>) {
  const settings = getEventTypeSettings(value);

  const updatePayment = (payment: Partial<EventTypeSettings["payment"]>) => {
    onChange(
      updateEventTypeSettingsSection(value, "payment", (currentPayment) => ({
        ...currentPayment,
        ...payment
      }))
    );
  };

  const updatePrimaryLocation = (locationType: string, locationValue: string | null) => {
    const nextValue = {
      ...value,
      locationType,
      locationValue
    };

    onChange(
      updateEventTypeSettings(nextValue, (nextSettings) => ({
        ...nextSettings,
        locations: replacePrimaryLocation(nextSettings.locations, locationType, locationValue)
      }))
    );
  };

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <EventTypeTabSection
        title={<Trans>Service basics</Trans>}
        description={<Trans>Only the details clients need to choose and book this service.</Trans>}
      >
        <EventTypeImageUpload eventTypeId={eventTypeId} imageUrl={imageUrl} />
        <div className="grid gap-4 md:grid-cols-2">
          <TextField
            name="title"
            label={t`Service name`}
            description={t`Use the name clients already know, like "Gel manicure".`}
            required={true}
            value={value.title}
            onChange={(title) => onChange({ ...value, title, slug: value.slug || slugify(title) })}
          />
          <NumberField
            name="durationMinutes"
            label={t`How long it takes`}
            description={t`Minutes blocked in your calendar.`}
            minValue={5}
            maxValue={1440}
            value={value.durationMinutes}
            onChange={(durationMinutes) => {
              const nextDuration = durationMinutes ?? 30;
              onChange(
                updateEventTypeSettings({ ...value, durationMinutes: nextDuration }, (nextSettings) => ({
                  ...nextSettings,
                  durationOptions: ensureDurationOption(nextSettings.durationOptions, nextDuration)
                }))
              );
            }}
          />
        </div>
        <TextAreaField
          name="description"
          label={t`Short description`}
          description={t`A simple note clients see before they book.`}
          lines={3}
          value={value.description ?? ""}
          onChange={(description) => onChange({ ...value, description: description || null })}
        />
        <div className="grid gap-4 md:grid-cols-2">
          <NumberField
            name="paymentPrice"
            label={t`Price`}
            description={t`Leave empty if the price changes per client.`}
            minValue={0}
            step={0.01}
            decimalPlaces={2}
            allowEmpty={true}
            value={settings.payment.price ?? undefined}
            onChange={(price) => updatePayment({ price: price ?? null })}
          />
          <NumberField
            name="paymentDepositAmount"
            label={t`Deposit`}
            description={t`Optional amount clients pay to secure the booking.`}
            minValue={0}
            step={0.01}
            decimalPlaces={2}
            allowEmpty={true}
            value={settings.payment.depositAmount ?? undefined}
            onChange={(depositAmount) =>
              updatePayment({ depositAmount: depositAmount ?? null, requiresDeposit: depositAmount !== null })
            }
          />
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <LocationTypeSelect
            value={value.locationType ?? ""}
            onChange={(locationType) => updatePrimaryLocation(locationType, value.locationValue ?? null)}
          />
          <TextField
            name="locationValue"
            label={t`Address, phone number, or video link`}
            description={t`Leave blank if you confirm the exact place with the client later.`}
            value={value.locationValue ?? ""}
            onChange={(locationValue) => updatePrimaryLocation(value.locationType ?? "link", locationValue || null)}
          />
        </div>
      </EventTypeTabSection>
    </FormValidationContext.Provider>
  );
}

function AdvancedServiceSettings({ tabProps }: Readonly<{ tabProps: EventTypeTabProps }>) {
  const [expanded, setExpanded] = useState(false);
  const showTeamSettings = Boolean(tabProps.value.teamId);

  return (
    <Collapsible open={expanded} onOpenChange={setExpanded}>
      <div className="rounded-lg border bg-card">
        <CollapsibleTrigger
          render={
            <Button
              type="button"
              variant="ghost"
              className="flex h-auto w-full items-start justify-between gap-4 rounded-lg p-4 text-left hover:bg-muted/60"
            >
              <span className="grid gap-1">
                <span className="font-semibold">
                  <Trans>Advanced settings</Trans>
                </span>
                <span className="text-sm font-normal text-muted-foreground">
                  <Trans>
                    Fine-tune links, hours, breathing room, calendars, client questions, and developer options.
                  </Trans>
                </span>
              </span>
              <ChevronDownIcon
                className={`mt-1 size-4 shrink-0 transition-transform ${expanded ? "rotate-180" : ""}`}
              />
            </Button>
          }
        />
        <CollapsibleContent>
          <div className="grid gap-5 border-t p-4 md:p-5">
            <ServiceAdvancedBasics {...tabProps} />
            <EventTypeAvailabilityTab {...tabProps} />
            <EventTypeLimitsTab {...tabProps} />
            <EventTypeAdvancedTab {...tabProps} />
            <EventTypeRecurringTab {...tabProps} />
            {showTeamSettings && <EventTypeTeamTab {...tabProps} />}
            <EventTypeAppsTab {...tabProps} />
            <EventTypeWebhooksTab eventTypeId={tabProps.eventTypeId} />
          </div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}

function ServiceAdvancedBasics({ value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const [durationOptionsText, setDurationOptionsText] = useState(formatDurationOptions(settings.durationOptions));
  const bookerLayouts = [
    { value: "month", label: t`Month` },
    { value: "week", label: t`Week` },
    { value: "column", label: t`Column` }
  ];

  useEffect(() => {
    setDurationOptionsText(formatDurationOptions(getEventTypeSettings(value).durationOptions));
  }, [value]);

  const updateSettings = (updater: Parameters<typeof updateEventTypeSettings>[1]) =>
    onChange(updateEventTypeSettings(value, updater));

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <EventTypeTabSection
        title={<Trans>Booking link and page</Trans>}
        description={<Trans>Change the booking link name, optional durations, and public page layout.</Trans>}
      >
        <div className="grid gap-4 md:grid-cols-2">
          <TextField
            name="slug"
            label={t`Link name`}
            description={t`This becomes the last part of the public booking link.`}
            required={true}
            value={value.slug}
            onChange={(slug) => onChange({ ...value, slug: slugify(slug) })}
          />
          <TextField
            name="durationOptions"
            label={t`Other time choices`}
            description={t`Optional comma-separated minutes clients can choose instead.`}
            value={durationOptionsText}
            onChange={setDurationOptionsText}
            onBlur={() => {
              const durationOptions = parseDurationOptions(durationOptionsText, value.durationMinutes);
              updateSettings((nextSettings) => ({ ...nextSettings, durationOptions }));
              setDurationOptionsText(formatDurationOptions(durationOptions));
            }}
          />
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <SelectField
            name="bookerLayout"
            label={t`Client page layout`}
            items={bookerLayouts}
            value={settings.bookerLayout}
            onValueChange={(bookerLayout) =>
              updateSettings((nextSettings) => ({ ...nextSettings, bookerLayout: bookerLayout ?? "month" }))
            }
          >
            <SelectTrigger>
              <SelectValue>
                {(bookerLayout: string) => bookerLayouts.find((item) => item.value === bookerLayout)?.label}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {bookerLayouts.map((bookerLayout) => (
                <SelectItem key={bookerLayout.value} value={bookerLayout.value}>
                  {bookerLayout.label}
                </SelectItem>
              ))}
            </SelectContent>
          </SelectField>
          <TextField
            name="eventColor"
            label={t`Booking page color`}
            type="color"
            value={settings.eventColor ?? "#2563eb"}
            onChange={(eventColor) => updateSettings((nextSettings) => ({ ...nextSettings, eventColor }))}
          />
        </div>
        <TextAreaField
          name="locations"
          label={t`Other places clients can choose`}
          description={t`One per line, like phone or in-person: 12 Main Road.`}
          lines={3}
          value={formatLocations(settings.locations)}
          onChange={(locationsText) => {
            const locations = parseLocations(locationsText);
            const primaryLocation = locations[0];
            onChange({
              ...updateEventTypeSettings(value, (nextSettings) => ({ ...nextSettings, locations })),
              locationType: primaryLocation?.type ?? value.locationType,
              locationValue: primaryLocation?.value ?? value.locationValue
            });
          }}
        />
      </EventTypeTabSection>
    </FormValidationContext.Provider>
  );
}

function ensureDurationOption(options: number[], durationMinutes: number) {
  return [...new Set([durationMinutes, ...options])].sort((left, right) => left - right);
}

function parseDurationOptions(value: string, durationMinutes: number) {
  const options = value
    .split(",")
    .map((option) => Number(option.trim()))
    .filter((option) => Number.isInteger(option) && option >= 5 && option <= 1440);

  return ensureDurationOption(options.length > 0 ? options : [durationMinutes], durationMinutes);
}

function formatDurationOptions(options: number[]) {
  return options.join(", ");
}

function replacePrimaryLocation(
  locations: Array<{ type: string; value: string | null; displayLocationPubliclyToTeam: boolean }>,
  type: string,
  value: string | null
) {
  const primaryLocation = { type, value: value?.trim() || null, displayLocationPubliclyToTeam: false };
  return locations.length === 0 ? [primaryLocation] : [primaryLocation, ...locations.slice(1)];
}

function formatLocations(locations: Array<{ type: string; value: string | null }>) {
  return locations.map((location) => `${location.type}${location.value ? `: ${location.value}` : ""}`).join("\n");
}

function parseLocations(value: string) {
  return value
    .split("\n")
    .map((line) => {
      const [type, ...rest] = line.split(":");
      return { type: type.trim(), value: rest.join(":").trim() || null, displayLocationPubliclyToTeam: false };
    })
    .filter((location) => location.type.length > 0);
}

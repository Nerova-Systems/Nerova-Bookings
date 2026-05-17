/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { useEffect, useState } from "react";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { LocationTypeSelect } from "../LocationTypeSelect";
import { getEventTypeSettings, slugify, updateEventTypeSettings } from "../schedulingTypes";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeSetupTab({ value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const [durationOptionsText, setDurationOptionsText] = useState(formatDurationOptions(settings.durationOptions));

  useEffect(() => {
    setDurationOptionsText(formatDurationOptions(getEventTypeSettings(value).durationOptions));
  }, [value]);

  const bookerLayouts = [
    { value: "month", label: t`Month` },
    { value: "week", label: t`Week` },
    { value: "column", label: t`Column` }
  ];

  const updateSettings = (updater: Parameters<typeof updateEventTypeSettings>[1]) =>
    onChange(updateEventTypeSettings(value, updater));

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
      <div className="grid gap-5">
        <EventTypeTabSection
          title={<Trans>Setup</Trans>}
          description={<Trans>Define the public booking page details people see before they choose a time.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <TextField
              name="title"
              label={t`Title`}
              required={true}
              value={value.title}
              onChange={(title) => onChange({ ...value, title, slug: value.slug || slugify(title) })}
            />
            <TextField
              name="slug"
              label={t`Slug`}
              required={true}
              value={value.slug}
              onChange={(slug) => onChange({ ...value, slug: slugify(slug) })}
            />
          </div>
          <TextAreaField
            name="description"
            label={t`Description`}
            lines={4}
            value={value.description ?? ""}
            onChange={(description) => onChange({ ...value, description: description || null })}
          />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Duration</Trans>}
          description={<Trans>Set how long this booking reserves on the calendar.</Trans>}
        >
          <NumberField
            name="durationMinutes"
            label={t`Duration`}
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
          <TextField
            name="durationOptions"
            label={t`Duration options`}
            value={durationOptionsText}
            onChange={setDurationOptionsText}
            onBlur={() => {
              const durationOptions = parseDurationOptions(durationOptionsText, value.durationMinutes);
              updateSettings((nextSettings) => ({ ...nextSettings, durationOptions }));
              setDurationOptionsText(formatDurationOptions(durationOptions));
            }}
          />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Booking page</Trans>}
          description={<Trans>Choose the booker layout and accent color for this event type.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SelectField
              name="bookerLayout"
              label={t`Booker layout`}
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
              label={t`Event color`}
              type="color"
              value={settings.eventColor ?? "#2563eb"}
              onChange={(eventColor) =>
                updateSettings((nextSettings) => ({
                  ...nextSettings,
                  eventColor
                }))
              }
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Locations</Trans>}
          description={<Trans>Set the primary location and optional alternatives shown to bookers.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <LocationTypeSelect
              value={value.locationType ?? ""}
              onChange={(locationType) => updatePrimaryLocation(locationType, value.locationValue ?? null)}
            />
            <TextField
              name="locationValue"
              label={t`Primary location`}
              value={value.locationValue ?? ""}
              onChange={(locationValue) => updatePrimaryLocation(value.locationType ?? "link", locationValue || null)}
            />
          </div>
          <TextAreaField
            name="locations"
            label={t`Location list`}
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
      </div>
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
  locations: Array<{ type: string; value: string | null }>,
  type: string,
  value: string | null
) {
  const primaryLocation = { type, value: value?.trim() || null };
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
      return { type: type.trim(), value: rest.join(":").trim() || null };
    })
    .filter((location) => location.type.length > 0);
}

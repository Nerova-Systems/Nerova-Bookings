import { t } from "@lingui/core/macro";
import { GlobeIcon } from "lucide-react";
import { type ReactNode, useMemo } from "react";

import { SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from "./Select";
import { SelectField } from "./SelectField";

interface TimeZoneGroup {
  continent: string;
  zones: { id: string; label: string }[];
}

const continentOrder = [
  "Africa",
  "America",
  "Antarctica",
  "Arctic",
  "Asia",
  "Atlantic",
  "Australia",
  "Europe",
  "Indian",
  "Pacific"
];

// Display the Ukrainian spelling "Kyiv" instead of the Russian-derived "Kiev" still shipped by the IANA tz database.
const cityLabelOverrides: Record<string, string> = {
  Kiev: "Kyiv"
};

function buildTimeZoneGroups(): TimeZoneGroup[] {
  const zones = Intl.supportedValuesOf("timeZone");
  const now = new Date();

  const entries = zones
    .filter((zone) => zone.includes("/"))
    .map((zone) => {
      const parts = zone.split("/");
      const continent = parts[0];
      const rawCity = parts.slice(1).join("/").replace(/_/g, " ");
      const city = cityLabelOverrides[rawCity] ?? rawCity;
      const offset =
        new Intl.DateTimeFormat("en-US", { timeZone: zone, timeZoneName: "longOffset" })
          .formatToParts(now)
          .find((p) => p.type === "timeZoneName")?.value ?? "";

      return { id: zone, label: `${city} (${offset})`, continent };
    })
    .sort((a, b) => a.label.localeCompare(b.label));

  const groups: Record<string, { id: string; label: string }[]> = {};
  for (const entry of entries) {
    if (!groups[entry.continent]) groups[entry.continent] = [];
    groups[entry.continent].push({ id: entry.id, label: entry.label });
  }

  return continentOrder.filter((c) => groups[c]).map((continent) => ({ continent, zones: groups[continent] }));
}

let cachedGroups: TimeZoneGroup[] | null = null;
function getTimeZoneGroups(): TimeZoneGroup[] {
  if (!cachedGroups) {
    cachedGroups = buildTimeZoneGroups();
  }
  return cachedGroups;
}

export interface TimeZonePickerProps {
  name?: string;
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  placeholder?: string;
  startIcon?: ReactNode;
  value?: string | null;
  onValueChange?: (value: string | null) => void;
  className?: string;
  disabled?: boolean;
  readOnly?: boolean;
}

export function TimeZonePicker({
  placeholder = t`Select time zone`,
  startIcon = <GlobeIcon />,
  value,
  onValueChange,
  ...props
}: Readonly<TimeZonePickerProps>) {
  const groups = useMemo(() => getTimeZoneGroups(), []);
  const items = useMemo(
    () => groups.flatMap(({ zones }) => zones.map((zone) => ({ value: zone.id, label: zone.label }))),
    [groups]
  );

  return (
    <SelectField items={items} value={value ?? undefined} onValueChange={(v) => onValueChange?.(v ?? null)} {...props}>
      <SelectTrigger>
        {startIcon}
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {groups.map(({ continent, zones }) => (
          <SelectGroup key={continent} className="p-0">
            <SelectLabel className="sticky -top-1 z-10 -mx-1 bg-muted px-3 pt-2.5 pb-1.5 font-semibold text-foreground">
              {continent}
            </SelectLabel>
            {zones.map((zone) => (
              <SelectItem key={zone.id} value={zone.id}>
                {zone.label}
              </SelectItem>
            ))}
          </SelectGroup>
        ))}
      </SelectContent>
    </SelectField>
  );
}

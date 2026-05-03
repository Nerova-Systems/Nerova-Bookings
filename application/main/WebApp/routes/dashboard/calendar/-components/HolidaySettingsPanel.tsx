import { Switch } from "@repo/ui/components/Switch";
import { Globe2Icon } from "lucide-react";
import { toast } from "sonner";

import type { HolidaySettings, PublicHoliday } from "@/shared/lib/appointmentsApi";

import { useUpdateHolidaySettings } from "@/shared/lib/availabilitySettingsApi";

interface HolidaySettingsPanelProps {
  holidaySettings?: HolidaySettings;
}

export function HolidaySettingsPanel({ holidaySettings }: HolidaySettingsPanelProps) {
  const updateHolidaySettings = useUpdateHolidaySettings();
  if (!holidaySettings) return null;

  const openHolidayIds = holidaySettings.holidays.filter((holiday) => holiday.isOpen).map((holiday) => holiday.id);

  const updateCountry = (countryCode: string) => {
    updateHolidaySettings.mutate(
      { countryCode, openHolidayIds: [] },
      {
        onSuccess: () => toast.success("Public holiday country updated."),
        onError: (error) => toast.error(error instanceof Error ? error.message : "Could not update public holidays.")
      }
    );
  };

  const toggleHoliday = (holiday: PublicHoliday, isOpen: boolean) => {
    const nextOpenHolidayIds = isOpen
      ? [...openHolidayIds, holiday.id]
      : openHolidayIds.filter((holidayId) => holidayId !== holiday.id);

    updateHolidaySettings.mutate(
      {
        countryCode: holidaySettings.countryCode,
        openHolidayIds: Array.from(new Set(nextOpenHolidayIds))
      },
      {
        onSuccess: () => toast.success(isOpen ? "Public holiday marked open." : "Public holiday marked closed."),
        onError: (error) => toast.error(error instanceof Error ? error.message : "Could not update public holiday.")
      }
    );
  };

  return (
    <section className="rounded-lg border border-border">
      <div className="flex flex-wrap items-center gap-3 border-b border-border px-3 py-3">
        <div className="flex min-w-0 items-center gap-2">
          <Globe2Icon className="size-4 text-muted-foreground" />
          <div>
            <h3 className="text-sm font-medium">Public holidays</h3>
            <p className="text-xs text-muted-foreground">
              Closed by default. Toggle a holiday on when the business is open.
            </p>
          </div>
        </div>
        <select
          className="ml-auto h-9 rounded-md border border-input bg-background px-2.5 text-sm outline-ring focus-visible:outline-2 focus-visible:outline-offset-2"
          value={holidaySettings.countryCode}
          disabled={updateHolidaySettings.isPending}
          onChange={(event) => updateCountry(event.target.value)}
        >
          {holidaySettings.countries.map((country) => (
            <option key={country.code} value={country.code}>
              {country.code} {country.name}
            </option>
          ))}
        </select>
      </div>
      <div className="max-h-72 overflow-y-auto">
        {holidaySettings.holidays.map((holiday) => (
          <HolidayRow
            key={holiday.id}
            holiday={holiday}
            disabled={updateHolidaySettings.isPending}
            onToggle={toggleHoliday}
          />
        ))}
      </div>
    </section>
  );
}

function HolidayRow({
  holiday,
  disabled,
  onToggle
}: {
  holiday: PublicHoliday;
  disabled: boolean;
  onToggle: (holiday: PublicHoliday, isOpen: boolean) => void;
}) {
  return (
    <div className="flex items-center gap-3 border-b border-border px-3 py-3 last:border-0">
      <div className="min-w-0">
        <div className="truncate text-sm font-medium">{holiday.label}</div>
        <div className="text-xs text-muted-foreground">
          {formatHolidayDate(holiday.date)} - {holiday.isOpen ? "Open" : "Closed"}
        </div>
      </div>
      <Switch
        className="ml-auto"
        checked={holiday.isOpen}
        disabled={disabled}
        onCheckedChange={(isOpen) => onToggle(holiday, isOpen)}
      />
    </div>
  );
}

function formatHolidayDate(date: string) {
  return new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short", year: "numeric" }).format(
    new Date(`${date}T00:00:00`)
  );
}

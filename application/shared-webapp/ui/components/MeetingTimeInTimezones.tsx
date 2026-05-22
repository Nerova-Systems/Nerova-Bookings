import { useLingui } from "@lingui/react/macro";
import { ClockIcon } from "lucide-react";

import { cn } from "../utils";
import { Popover, PopoverContent, PopoverTrigger } from "./Popover";

/**
 * Displays a meeting time in the viewer's timezone with a popover showing it in additional timezones.
 * Ported from cal.com `packages/ui/components/popover/MeetingTimeInTimezones.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface TimezoneEntry {
  timezone: string;
  /** Display label override (e.g. "My timezone" or the user's name). */
  label?: string;
}

interface MeetingTimeInTimezonesProps {
  /** ISO 8601 date-time string of the meeting. */
  startTime: string;
  /** Duration in minutes. */
  duration: number;
  /** Timezones to display. First entry is shown in the trigger. */
  timezones?: TimezoneEntry[];
  className?: string;
}

function formatTimeRange(start: string, duration: number, timezone: string): string {
  try {
    const startDate = new Date(start);
    const endDate = new Date(startDate.getTime() + duration * 60_000);

    const fmt = (d: Date) =>
      new Intl.DateTimeFormat("en-US", {
        hour: "numeric",
        minute: "2-digit",
        timeZone: timezone
      }).format(d);

    const tz = new Intl.DateTimeFormat("en-US", {
      timeZoneName: "short",
      timeZone: timezone
    })
      .formatToParts(startDate)
      .find((p) => p.type === "timeZoneName")?.value;

    return `${fmt(startDate)} – ${fmt(endDate)} ${tz ?? ""}`;
  } catch {
    return start;
  }
}

export function MeetingTimeInTimezones({
  startTime,
  duration,
  timezones = [],
  className
}: MeetingTimeInTimezonesProps) {
  const { t } = useLingui();

  if (timezones.length === 0) return null;

  const [primary, ...rest] = timezones;

  return (
    <Popover>
      <PopoverTrigger
        className={cn(
          "inline-flex cursor-pointer items-center gap-1.5 text-sm text-muted-foreground underline underline-offset-2 hover:text-foreground",
          className
        )}
        aria-label={t`Show time in all timezones`}
      >
        <ClockIcon className="size-3.5" />
        {formatTimeRange(startTime, duration, primary.timezone)}
      </PopoverTrigger>
      {rest.length > 0 && (
        <PopoverContent className="w-auto min-w-[14rem]">
          <div className="flex flex-col gap-2">
            {timezones.map(({ timezone, label }, i) => (
              <div key={i} className="flex flex-col gap-0.5">
                {label && <span className="text-xs font-medium text-foreground">{label}</span>}
                <span className="text-xs text-muted-foreground">{formatTimeRange(startTime, duration, timezone)}</span>
              </div>
            ))}
          </div>
        </PopoverContent>
      )}
    </Popover>
  );
}

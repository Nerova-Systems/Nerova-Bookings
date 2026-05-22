import { Trans } from "@lingui/react/macro";

import { cn } from "../utils";
import { Switch } from "./Switch";

/**
 * A toggle row for enabling/disabling calendar visibility in settings.
 * Ported from cal.com `packages/ui/components/calendar-switch/CalendarSwitch.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface CalendarSwitchProps {
  /** Calendar display name. */
  name: string;
  /** Calendar type / integration label (e.g. "Google Calendar"). */
  type?: string;
  /** Calendar source description (e.g. the email address). */
  externalId?: string;
  checked?: boolean;
  defaultChecked?: boolean;
  onCheckedChange?: (checked: boolean) => void;
  disabled?: boolean;
  isLoading?: boolean;
  destination?: boolean;
  className?: string;
}

export function CalendarSwitch({
  name,
  type,
  externalId,
  checked,
  defaultChecked,
  onCheckedChange,
  disabled,
  isLoading,
  destination,
  className
}: CalendarSwitchProps) {
  return (
    <div data-slot="calendar-switch" className={cn("flex items-center justify-between gap-4 py-3", className)}>
      <div className="flex min-w-0 flex-col gap-0.5">
        <div className="flex items-center gap-2">
          <span className="truncate text-sm font-medium text-foreground">{name}</span>
          {destination && (
            <span className="shrink-0 rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary">
              <Trans>Default</Trans>
            </span>
          )}
        </div>
        {(type || externalId) && (
          <span className="truncate text-xs text-muted-foreground">
            {type && externalId ? `${type} · ${externalId}` : (type ?? externalId)}
          </span>
        )}
      </div>
      <Switch
        checked={checked}
        defaultChecked={defaultChecked}
        onCheckedChange={onCheckedChange}
        disabled={disabled || isLoading}
        className="shrink-0"
      />
    </div>
  );
}

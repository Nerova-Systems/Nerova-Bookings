import { Trans } from "@lingui/react/macro";

import { cn } from "../utils";
import { Switch } from "./Switch";

/**
 * A settings row with a switch, title, description, and optional children (expanded content).
 * Ported from cal.com `packages/ui/components/switch/SettingsToggle.tsx` (cf2a55c).
 *
 * Prop deviation: `labelClassName` was not in cal.com but added here for layout flexibility.
 * `onCheckedChange` maps to cal.com's `onToggle`. Both names accepted.
 *
 * Note: Nerova `SwitchField` covers field-in-form usage. This component targets
 * full-width settings rows (title + description + toggle inline, with optional
 * expanded content below). Confirmed distinct semantics from SwitchField.
 */
interface SettingsToggleProps {
  title: React.ReactNode;
  description?: React.ReactNode;
  /** Checked state */
  checked?: boolean;
  defaultChecked?: boolean;
  onCheckedChange?: (checked: boolean) => void;
  disabled?: boolean;
  /** Content revealed below the toggle row when checked (or always if not conditional). */
  children?: React.ReactNode;
  /** Whether children are shown based on the checked state. @default false */
  hideChildrenWhenUnchecked?: boolean;
  loading?: boolean;
  className?: string;
  labelClassName?: string;
  switchContainerClassName?: string;
  "data-testid"?: string;
}

export function SettingsToggle({
  title,
  description,
  checked,
  defaultChecked,
  onCheckedChange,
  disabled,
  children,
  hideChildrenWhenUnchecked = false,
  loading,
  className,
  labelClassName,
  switchContainerClassName,
  "data-testid": dataTestId
}: SettingsToggleProps) {
  const showChildren = !hideChildrenWhenUnchecked || checked;

  return (
    <div data-slot="settings-toggle" className={cn("flex flex-col gap-4", className)} data-testid={dataTestId}>
      <div className={cn("flex items-start justify-between gap-4", switchContainerClassName)}>
        <div className={cn("flex flex-col gap-1", labelClassName)}>
          <span className="text-sm font-semibold text-foreground">{title}</span>
          {description && <p className="text-sm text-muted-foreground">{description}</p>}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {loading && (
            <span className="text-xs text-muted-foreground">
              <Trans>Saving…</Trans>
            </span>
          )}
          <Switch
            checked={checked}
            defaultChecked={defaultChecked}
            onCheckedChange={onCheckedChange}
            disabled={disabled || loading}
          />
        </div>
      </div>
      {children && showChildren && <div className="pl-0">{children}</div>}
    </div>
  );
}

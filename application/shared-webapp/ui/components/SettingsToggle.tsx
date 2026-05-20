import type React from "react";

import { cn } from "../utils";
import { Label } from "./Label";
import { Switch } from "./Switch";

export function SettingsToggle({
  title,
  description,
  checked,
  disabled,
  children,
  onCheckedChange,
  className,
  contentClassName,
  hideSwitch,
  badge,
  "data-testid": dataTestId
}: Readonly<{
  title: React.ReactNode;
  description?: React.ReactNode;
  checked: boolean;
  disabled?: boolean;
  children?: React.ReactNode;
  onCheckedChange?: (checked: boolean) => void;
  className?: string;
  contentClassName?: string;
  hideSwitch?: boolean;
  badge?: React.ReactNode;
  "data-testid"?: string;
}>) {
  return (
    <div className={cn("rounded-lg border bg-background", checked && children ? "overflow-hidden" : "", className)}>
      <div className="flex items-center justify-between gap-4 px-4 py-5 sm:px-6">
        <div className="min-w-0">
          <div className="flex min-w-0 items-center gap-2">
            <Label className="text-sm leading-tight font-semibold">{title}</Label>
            {badge}
          </div>
          {description && <div className="mt-1 text-sm leading-normal text-muted-foreground">{description}</div>}
        </div>
        {!hideSwitch && (
          <Switch checked={checked} disabled={disabled} data-testid={dataTestId} onCheckedChange={onCheckedChange} />
        )}
      </div>
      {checked && children && <div className={cn("border-t p-4 sm:p-6", contentClassName)}>{children}</div>}
    </div>
  );
}

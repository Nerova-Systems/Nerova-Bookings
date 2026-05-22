import { cn } from "../utils";
import { Popover, PopoverContent, PopoverTrigger } from "./Popover";

/**
 * A popover with animated open/close transitions, used for filter chips and quick-form popovers.
 * Ported from cal.com `packages/ui/components/popover/AnimatedPopover.tsx` (cf2a55c).
 *
 * Deviation: cal.com uses Radix Popover with custom CSS animation classes.
 * Nerova's PopoverContent already has open/close animations via `data-open` / `data-closed`.
 * This component adds a `chipLabel` prop for the filter-chip trigger pattern.
 *
 * No structural deviations; animation is handled by the existing Nerova Popover.
 */
interface AnimatedPopoverProps {
  /** Content shown inside the popover trigger chip/button. */
  chipLabel?: React.ReactNode;
  /** Trigger element to override the default chip. If provided, `chipLabel` is ignored. */
  trigger?: React.ReactNode;
  children: React.ReactNode;
  /** @default "bottom" */
  side?: "top" | "bottom" | "left" | "right";
  /** @default "start" */
  align?: "start" | "center" | "end";
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  className?: string;
  contentClassName?: string;
}

export function AnimatedPopover({
  chipLabel,
  trigger,
  children,
  side = "bottom",
  align = "start",
  open,
  onOpenChange,
  className,
  contentClassName
}: AnimatedPopoverProps) {
  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger
        className={cn(
          "inline-flex h-[var(--control-height)] cursor-pointer items-center gap-1.5 rounded-md border border-border bg-background px-3 text-sm font-medium whitespace-nowrap outline-ring transition-colors hover:bg-muted focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 aria-expanded:bg-muted",
          className
        )}
      >
        {trigger ?? chipLabel}
      </PopoverTrigger>
      <PopoverContent side={side} align={align} className={cn("w-auto min-w-[14rem] p-2", contentClassName)}>
        {children}
      </PopoverContent>
    </Popover>
  );
}

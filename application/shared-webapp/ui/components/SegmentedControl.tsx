import { Tabs as TabsPrimitive } from "@base-ui/react/tabs";

import { cn } from "../utils";

/**
 * A segmented control for toggling between mutually exclusive view modes.
 * Visually distinct from HorizontalTabs (no underline; pill-style selected state).
 * Ported from cal.com `packages/ui/components/segmented-control/SegmentedControl.tsx` (cf2a55c).
 *
 * Implemented on top of Base UI Tabs with a pill visual treatment.
 * No prop deviations.
 */
function SegmentedControl({ className, ...props }: TabsPrimitive.Root.Props) {
  return <TabsPrimitive.Root data-slot="segmented-control" className={cn("inline-flex", className)} {...props} />;
}

function SegmentedControlList({ className, ...props }: TabsPrimitive.List.Props) {
  return (
    <TabsPrimitive.List
      data-slot="segmented-control-list"
      className={cn("inline-flex h-9 items-center gap-0.5 rounded-lg bg-muted p-1", className)}
      {...props}
    />
  );
}

function SegmentedControlItem({ className, ...props }: TabsPrimitive.Tab.Props) {
  return (
    <TabsPrimitive.Tab
      data-slot="segmented-control-item"
      className={cn(
        "inline-flex h-7 min-w-[4.5rem] cursor-pointer items-center justify-center rounded-md px-3 text-sm font-medium whitespace-nowrap outline-ring transition-all",
        "text-muted-foreground hover:text-foreground focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
        "data-selected:bg-background data-selected:text-foreground data-selected:shadow-xs",
        "disabled:pointer-events-none disabled:opacity-50",
        className
      )}
      {...props}
    />
  );
}

export { SegmentedControl, SegmentedControlList, SegmentedControlItem };

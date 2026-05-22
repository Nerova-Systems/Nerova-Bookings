import { Radio as RadioPrimitive } from "@base-ui/react/radio";
import { RadioGroup as RadioGroupPrimitive } from "@base-ui/react/radio-group";

import { cn } from "../utils";

/**
 * Card-style radio group where each item renders as a selectable card area.
 * Ported from cal.com `packages/ui/components/radio/RadioAreaGroup.tsx` (cf2a55c).
 *
 * Implements Base UI RadioGroup + Radio with card visual treatment.
 * No prop deviations.
 */
function RadioAreaGroup({ className, ...props }: RadioGroupPrimitive.Props) {
  return <RadioGroupPrimitive data-slot="radio-area-group" className={cn("grid gap-3", className)} {...props} />;
}

interface RadioAreaProps extends RadioPrimitive.Root.Props {
  children: React.ReactNode;
  className?: string;
}

function RadioArea({ className, children, ...props }: RadioAreaProps) {
  return (
    <RadioPrimitive.Root
      data-slot="radio-area"
      className={cn(
        "group/radio-area relative flex cursor-pointer rounded-lg border border-border bg-card p-4 shadow-xs outline-ring transition-colors",
        "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
        "hover:border-primary/50 hover:bg-accent/50",
        "data-checked:border-primary data-checked:bg-primary/5",
        "disabled:cursor-not-allowed disabled:opacity-50",
        className
      )}
      {...props}
    >
      {children}
    </RadioPrimitive.Root>
  );
}

function RadioAreaIndicator({ className, ...props }: RadioPrimitive.Indicator.Props) {
  return (
    <RadioPrimitive.Indicator
      data-slot="radio-area-indicator"
      className={cn(
        "flex size-4 shrink-0 items-center justify-center rounded-full border border-input",
        "group-data-checked/radio-area:border-primary group-data-checked/radio-area:bg-primary",
        className
      )}
      {...props}
    >
      <span className="size-2 rounded-full bg-white" />
    </RadioPrimitive.Indicator>
  );
}

export { RadioAreaGroup, RadioArea, RadioAreaIndicator };

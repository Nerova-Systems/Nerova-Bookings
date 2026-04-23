import { Toggle as TogglePrimitive } from "@base-ui/react/toggle";
import { cva, type VariantProps } from "class-variance-authority";

import { cn } from "../utils";

const toggleVariants = cva(
  "group/toggle inline-flex cursor-pointer items-center justify-center gap-1 rounded-md text-sm font-medium whitespace-nowrap outline-ring transition-[color,box-shadow] hover:bg-muted hover:text-foreground focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 active:bg-accent disabled:pointer-events-none disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-destructive/20 aria-pressed:bg-primary aria-pressed:text-primary-foreground dark:aria-invalid:ring-destructive/40 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default: "bg-transparent",
        outline: "border border-input bg-transparent shadow-xs hover:bg-muted"
      },
      size: {
        default: "h-[var(--control-height)] min-w-[var(--control-height)] px-2",
        sm: "h-[var(--control-height-sm)] min-w-[var(--control-height-sm)] px-1.5",
        lg: "h-[var(--control-height-lg)] min-w-[var(--control-height-lg)] px-2.5"
      }
    },
    defaultVariants: {
      variant: "default",
      size: "default"
    }
  }
);

function Toggle({
  className,
  variant = "default",
  size = "default",
  ...props
}: TogglePrimitive.Props & VariantProps<typeof toggleVariants>) {
  return <TogglePrimitive data-slot="toggle" className={cn(toggleVariants({ variant, size, className }))} {...props} />;
}

export { Toggle, toggleVariants };

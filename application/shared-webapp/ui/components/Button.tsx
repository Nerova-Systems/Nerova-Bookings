import { Button as ButtonPrimitive } from "@base-ui/react/button";
import { cva, type VariantProps } from "class-variance-authority";

import { cn } from "../utils";
import { Spinner } from "./Spinner";

const buttonVariants = cva(
  "group/button inline-flex cursor-pointer items-center justify-center rounded-md text-sm font-medium whitespace-nowrap transition-colors select-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 disabled:pointer-events-none disabled:opacity-50 aria-invalid:outline aria-invalid:outline-2 aria-invalid:outline-offset-2 aria-invalid:outline-destructive [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground outline-primary hover:bg-primary/80 active:bg-primary/70",
        outline:
          "border-border bg-white shadow-xs outline-ring hover:bg-muted hover:text-foreground active:bg-accent aria-expanded:bg-muted aria-expanded:text-foreground dark:border-input dark:bg-input/30 dark:hover:bg-input/50 dark:active:bg-input/60",
        secondary:
          "bg-white text-foreground outline-ring hover:bg-muted active:bg-accent aria-expanded:bg-muted dark:bg-input/30 dark:hover:bg-input/50 dark:active:bg-input/60",
        ghost:
          "outline-ring hover:bg-muted hover:text-foreground active:bg-accent aria-expanded:bg-muted aria-expanded:text-foreground dark:hover:bg-muted/50 dark:active:bg-muted/70",
        destructive:
          "bg-destructive text-destructive-foreground outline-destructive hover:bg-destructive/90 active:bg-destructive/80",
        link: "text-primary underline-offset-4 outline-ring hover:underline active:opacity-70"
      },
      size: {
        default:
          "h-[var(--control-height)] w-fit min-w-[var(--control-height)] gap-1.5 px-6 in-data-[slot=button-group]:rounded-md has-data-[icon=inline-end]:pr-4 has-data-[icon=inline-start]:pl-4 max-sm:w-full",
        xs: "h-[var(--control-height-xs)] min-w-[var(--control-height-xs)] gap-1 rounded-[min(var(--radius-md),0.5rem)] px-3 text-xs in-data-[slot=button-group]:rounded-md has-data-[icon=inline-end]:pr-2 has-data-[icon=inline-start]:pl-2 max-sm:w-full [&_svg:not([class*='size-'])]:size-3",
        sm: "h-[var(--control-height-sm)] min-w-[var(--control-height-sm)] gap-1 rounded-[min(var(--radius-md),0.625rem)] px-4 in-data-[slot=button-group]:rounded-md has-data-[icon=inline-end]:pr-3 has-data-[icon=inline-start]:pl-3 max-sm:w-full",
        lg: "h-[var(--control-height-lg)] w-fit min-w-[var(--control-height-lg)] gap-1.5 px-7 has-data-[icon=inline-end]:pr-5 has-data-[icon=inline-start]:pl-5 max-sm:w-full",
        icon: "h-[var(--control-height)] w-[var(--control-height)] min-w-[var(--control-height)] p-0",
        "icon-xs":
          "h-[var(--control-height-xs)] w-[var(--control-height-xs)] min-w-[var(--control-height-xs)] rounded-[min(var(--radius-md),0.5rem)] p-0 in-data-[slot=button-group]:rounded-md [&_svg:not([class*='size-'])]:size-3",
        "icon-sm":
          "h-[var(--control-height-sm)] w-[var(--control-height-sm)] min-w-[var(--control-height-sm)] rounded-[min(var(--radius-md),0.625rem)] p-0 in-data-[slot=button-group]:rounded-md",
        "icon-lg": "h-[var(--control-height-lg)] w-[var(--control-height-lg)] min-w-[var(--control-height-lg)] p-0"
      }
    },
    defaultVariants: {
      variant: "default",
      size: "default"
    }
  }
);

type ButtonProps = ButtonPrimitive.Props &
  VariantProps<typeof buttonVariants> & {
    isPending?: boolean;
  };

function Button({
  className,
  variant = "default",
  size = "default",
  isPending,
  disabled,
  children,
  ...props
}: ButtonProps) {
  return (
    <ButtonPrimitive
      data-slot="button"
      className={cn(buttonVariants({ variant, size, className }))}
      disabled={isPending || disabled}
      {...props}
    >
      {isPending && <Spinner />}
      {children}
    </ButtonPrimitive>
  );
}

export { Button, buttonVariants };

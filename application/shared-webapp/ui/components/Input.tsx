import type * as React from "react";

import { Input as InputPrimitive } from "@base-ui/react/input";

import { cn } from "../utils";

function Input({ className, type, ...props }: React.ComponentProps<"input">) {
  return (
    <InputPrimitive
      type={type}
      data-slot="input"
      className={cn(
        "h-[var(--control-height)] w-full min-w-0 rounded-md border border-input bg-white px-2.5 py-1 text-sm shadow-xs outline-ring transition-[color,box-shadow] file:inline-flex file:h-[var(--control-height-sm)] file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:outline aria-invalid:outline-2 aria-invalid:outline-offset-2 aria-invalid:outline-destructive aria-invalid:focus-visible:shadow-error-halo dark:bg-input/30",
        className
      )}
      {...props}
    />
  );
}

export { Input };

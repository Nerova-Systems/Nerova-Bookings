import { Tooltip as TooltipPrimitive } from "@base-ui/react/tooltip";
import { createContext, use, useState } from "react";

import { cn } from "../utils";

function TooltipProvider({ delay = 300, ...props }: TooltipPrimitive.Provider.Props) {
  return <TooltipPrimitive.Provider data-slot="tooltip-provider" delay={delay} {...props} />;
}

// Context to pass setOpen from Tooltip to TooltipTrigger for tap-to-open functionality
const TooltipOpenContext = createContext<(() => void) | null>(null);

function Tooltip({ open: controlledOpen, onOpenChange, ...props }: TooltipPrimitive.Root.Props) {
  const [internalOpen, setInternalOpen] = useState(false);
  const isControlled = controlledOpen !== undefined;
  const open = isControlled ? controlledOpen : internalOpen;

  const handleOpenChange: TooltipPrimitive.Root.Props["onOpenChange"] = (newOpen, eventDetails) => {
    if (eventDetails.reason === "trigger-press") {
      // On hover-capable devices, ignore press entirely so clicks never affect tooltip visibility.
      // On touch devices, allow open via press but prevent close via press (we toggle via onClick instead).
      if (window.matchMedia("(hover: none)").matches) {
        if (!newOpen) {
          eventDetails.cancel();
          return;
        }
      } else {
        eventDetails.cancel();
        return;
      }
    }
    if (!isControlled) {
      setInternalOpen(newOpen);
    }
    onOpenChange?.(newOpen, eventDetails);
  };

  // Toggle function for tap-to-open on touch devices
  const toggleOpen = () => {
    const newOpen = !open;
    if (!isControlled) {
      setInternalOpen(newOpen);
    }
    // Call onOpenChange with a synthetic event details object for click/tap
    onOpenChange?.(newOpen, { reason: "trigger-press" } as TooltipPrimitive.Root.ChangeEventDetails);
  };

  return (
    <TooltipProvider>
      <TooltipOpenContext value={toggleOpen}>
        <TooltipPrimitive.Root data-slot="tooltip" open={open} onOpenChange={handleOpenChange} {...props} />
      </TooltipOpenContext>
    </TooltipProvider>
  );
}

function TooltipTrigger({ className, onClick, ...props }: TooltipPrimitive.Trigger.Props) {
  const toggleOpen = use(TooltipOpenContext);

  const handleClick: TooltipPrimitive.Trigger.Props["onClick"] = (event) => {
    // Only toggle tooltip on click for devices without hover capability (pure touch devices).
    // On hover-capable devices, the tooltip opens on hover and a click would cause a brief flash.
    if (window.matchMedia("(hover: none)").matches) {
      toggleOpen?.();
    }
    onClick?.(event);
  };

  return (
    <TooltipPrimitive.Trigger data-slot="tooltip-trigger" className={className} onClick={handleClick} {...props} />
  );
}

function TooltipContent({
  className,
  side = "top",
  sideOffset = 4,
  align = "center",
  alignOffset = 0,
  children,
  ...props
}: TooltipPrimitive.Popup.Props &
  Pick<TooltipPrimitive.Positioner.Props, "align" | "alignOffset" | "side" | "sideOffset">) {
  return (
    <TooltipPrimitive.Portal>
      <TooltipPrimitive.Positioner
        align={align}
        alignOffset={alignOffset}
        side={side}
        sideOffset={sideOffset}
        className="isolate z-50"
      >
        <TooltipPrimitive.Popup
          data-slot="tooltip-content"
          className={cn(
            "z-50 w-fit max-w-xs origin-(--transform-origin) rounded-md bg-foreground px-3 py-1.5 text-sm text-background data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95 data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-[state=delayed-open]:animate-in data-[state=delayed-open]:fade-in-0 data-[state=delayed-open]:zoom-in-95",
            className
          )}
          {...props}
        >
          {children}
          <TooltipPrimitive.Arrow className="z-50 size-2.5 translate-y-[calc(-50%-2px)] rotate-45 rounded-[0.125rem] bg-foreground fill-foreground data-[side=bottom]:top-1 data-[side=left]:top-1/2! data-[side=left]:-right-1 data-[side=left]:-translate-y-1/2 data-[side=right]:top-1/2! data-[side=right]:-left-1 data-[side=right]:-translate-y-1/2 data-[side=top]:-bottom-2.5" />
        </TooltipPrimitive.Popup>
      </TooltipPrimitive.Positioner>
    </TooltipPrimitive.Portal>
  );
}

export { Tooltip, TooltipTrigger, TooltipContent, TooltipProvider };

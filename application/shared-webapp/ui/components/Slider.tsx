import { Slider as SliderPrimitive } from "@base-ui/react/slider";
import { useEffect, useRef } from "react";

import { cn } from "../utils";

function Slider({ className, defaultValue, value, min = 0, max = 100, id, ...props }: SliderPrimitive.Root.Props) {
  const _values = Array.isArray(value) ? value : Array.isArray(defaultValue) ? defaultValue : [min, max];
  const rootRef = useRef<HTMLDivElement>(null);

  // BaseUI generates the focusable <input type="range"> id internally, so <Label htmlFor={id}> can't reach it.
  // Forward the consumer-supplied id onto that input so native label-for-input click-to-focus works.
  useEffect(() => {
    if (!id || !rootRef.current) return;
    const input = rootRef.current.querySelector<HTMLInputElement>("input[type='range']");
    if (input) input.id = id;
  }, [id]);

  return (
    <SliderPrimitive.Root
      ref={rootRef}
      className={cn("data-[orientation=horizontal]:w-full data-[orientation=vertical]:h-full", className)}
      data-slot="slider"
      defaultValue={defaultValue}
      value={value}
      min={min}
      max={max}
      thumbAlignment="edge"
      {...props}
    >
      <SliderPrimitive.Control className="relative flex w-full touch-none items-center select-none data-disabled:opacity-50 data-[orientation=vertical]:h-full data-[orientation=vertical]:min-h-40 data-[orientation=vertical]:w-auto data-[orientation=vertical]:flex-col">
        <SliderPrimitive.Track
          data-slot="slider-track"
          className="relative grow overflow-hidden rounded-full bg-foreground/20 select-none data-[orientation=horizontal]:h-1.5 data-[orientation=horizontal]:w-full data-[orientation=vertical]:h-full data-[orientation=vertical]:w-1.5"
        >
          <SliderPrimitive.Indicator data-slot="slider-range" className="block bg-primary select-none" />
        </SliderPrimitive.Track>
        {Array.from({ length: _values.length }, (_, index) => (
          <SliderPrimitive.Thumb
            data-slot="slider-thumb"
            key={index}
            className="relative block size-4 shrink-0 cursor-pointer rounded-full bg-primary shadow-xs outline-ring transition-shadow select-none after:absolute after:-inset-3.5 disabled:pointer-events-none disabled:opacity-50 has-focus:outline-2 has-focus:outline-offset-2"
          />
        ))}
      </SliderPrimitive.Control>
    </SliderPrimitive.Root>
  );
}

export { Slider };

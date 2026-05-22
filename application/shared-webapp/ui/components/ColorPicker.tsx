import { useLingui } from "@lingui/react/macro";
import { useCallback } from "react";
import { HexColorPicker, HexColorInput } from "react-colorful";

import { cn } from "../utils";

/**
 * Color picker built on `react-colorful`.
 * Ported from cal.com `packages/ui/components/color-picker/ColorPicker.tsx` (cf2a55c).
 *
 * Renders a hue/saturation/lightness picker with a hex text input below it.
 * No prop deviations.
 */
interface ColorPickerProps {
  value?: string;
  defaultValue?: string;
  onChange?: (color: string) => void;
  disabled?: boolean;
  className?: string;
  /** Override for the hex input aria-label. */
  hexInputLabel?: string;
}

export function ColorPicker({
  value,
  defaultValue = "#000000",
  onChange,
  disabled,
  className,
  hexInputLabel
}: ColorPickerProps) {
  const { t } = useLingui();

  const handleChange = useCallback(
    (color: string) => {
      onChange?.(color);
    },
    [onChange]
  );

  return (
    <div
      data-slot="color-picker"
      data-disabled={disabled || undefined}
      className={cn("flex flex-col gap-2 data-[disabled]:pointer-events-none data-[disabled]:opacity-50", className)}
    >
      <HexColorPicker color={value ?? defaultValue} onChange={handleChange} style={{ width: "100%" }} />
      <div className="flex items-center gap-2">
        <span className="text-sm text-muted-foreground">#</span>
        <HexColorInput
          color={value ?? defaultValue}
          onChange={handleChange}
          prefixed={false}
          aria-label={hexInputLabel ?? t`Hex color value`}
          className="h-[var(--control-height)] w-full rounded-md border border-input bg-background px-3 text-sm uppercase outline-ring focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2"
        />
      </div>
    </div>
  );
}

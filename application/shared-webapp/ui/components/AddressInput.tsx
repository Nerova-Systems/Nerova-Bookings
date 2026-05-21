import { useLingui } from "@lingui/react/macro";
import { MapPinIcon } from "lucide-react";

import { cn } from "../utils";
import { Input } from "./Input";

/**
 * An address autocomplete input.
 * Ported from cal.com `packages/ui/components/address/AddressInput.tsx` (cf2a55c).
 *
 * Deviation: cal.com ships Google Maps Places Autocomplete. That requires a Google Maps API key
 * which is not available in this project. This version renders a plain `<Input>` with appropriate
 * `autoComplete` attributes. Consumers who need autocomplete should layer a geo-suggestion library
 * (e.g. use-places-autocomplete or Mapbox) on top of the rendered input via the `onSuggest` prop.
 *
 * Visual and a11y semantics are identical to the original.
 */
interface AddressInputProps extends Omit<React.ComponentProps<"input">, "onChange" | "value"> {
  value?: string;
  onChange?: (value: string) => void;
  /** Called when a location is selected from a suggestion list (consumer-supplied). */
  onSuggest?: (value: string) => void;
  /** Show a map-pin icon prefix. @default true */
  showIcon?: boolean;
}

export function AddressInput({
  value,
  onChange,
  onSuggest: _onSuggest,
  showIcon = true,
  className,
  placeholder,
  ...props
}: AddressInputProps) {
  const { t } = useLingui();

  return (
    <div data-slot="address-input" className={cn("relative", className)}>
      {showIcon && (
        <MapPinIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
      )}
      <Input
        type="text"
        autoComplete="street-address"
        value={value}
        onChange={(e) => onChange?.(e.target.value)}
        placeholder={placeholder ?? t`Enter address`}
        className={cn(showIcon && "pl-9")}
        {...props}
      />
    </div>
  );
}

import type { Slider as SliderPrimitive } from "@base-ui/react/slider";

import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Field, FieldDescription, FieldError, FieldLabel } from "./Field";
import { LabelWithTooltip } from "./LabelWithTooltip";
import { Slider } from "./Slider";

export interface SliderFieldProps extends Omit<SliderPrimitive.Root.Props, "name"> {
  name?: string;
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  readOnly?: boolean;
}

export function SliderField({
  label,
  description,
  errorMessage,
  tooltip,
  className,
  name,
  readOnly,
  onValueChange,
  ...props
}: Readonly<SliderFieldProps>) {
  const { errors, isInvalid, clearNow } = useFieldError({ name, errorMessage });

  const handleValueChange: typeof onValueChange = (value, eventDetails) => {
    if (readOnly) return;
    clearNow();
    onValueChange?.(value, eventDetails);
  };

  return (
    <Field className={cn("flex flex-col gap-2", className)}>
      {label && (
        <FieldLabel htmlFor={name}>
          {tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label}
        </FieldLabel>
      )}
      <div className="flex h-[var(--control-height)] items-center">
        <Slider
          id={name}
          name={name}
          aria-invalid={isInvalid || undefined}
          aria-readonly={readOnly}
          onValueChange={handleValueChange}
          {...props}
        />
      </div>
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

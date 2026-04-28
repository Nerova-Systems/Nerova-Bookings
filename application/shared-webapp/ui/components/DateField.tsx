import { useState } from "react";

import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Field, FieldDescription, FieldError, FieldLabel } from "./Field";
import { Input } from "./Input";
import { LabelWithTooltip } from "./LabelWithTooltip";

export interface DateFieldProps extends Omit<React.ComponentProps<"input">, "className" | "onChange" | "type"> {
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  inputClassName?: string;
  onChange?: (value: string) => void;
}

export function DateField({
  label,
  description,
  errorMessage,
  tooltip,
  className,
  inputClassName,
  name,
  value,
  defaultValue,
  onChange,
  autoFocus,
  required,
  disabled,
  readOnly,
  ...props
}: Readonly<DateFieldProps>) {
  const { errors, isInvalid, markChanged, clearOnBlur } = useFieldError({ name, errorMessage });

  const [hasValue, setHasValue] = useState(!!(value ?? defaultValue));

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    markChanged();
    setHasValue(!!e.target.value);
    onChange?.(e.target.value);
  };

  return (
    <Field className={cn("flex flex-col", className)}>
      {label && (
        <FieldLabel htmlFor={name}>
          {tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label}
        </FieldLabel>
      )}
      <Input
        id={name}
        name={name}
        type="date"
        value={value}
        defaultValue={defaultValue}
        onChange={handleChange}
        onBlur={clearOnBlur}
        autoFocus={autoFocus}
        required={required}
        disabled={disabled}
        readOnly={readOnly}
        aria-invalid={isInvalid || undefined}
        className={cn(
          !hasValue && "text-muted-foreground",
          readOnly && "[&::-webkit-calendar-picker-indicator]:hidden [&::-webkit-inner-spin-button]:hidden",
          inputClassName
        )}
        {...props}
      />
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

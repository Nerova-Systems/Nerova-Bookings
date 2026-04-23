import type { Select as SelectPrimitive } from "@base-ui/react/select";

import { useContext } from "react";

import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Field, FieldDescription, FieldError } from "./Field";
import { FormValidationContext } from "./Form";
import { Label } from "./Label";
import { LabelWithTooltip } from "./LabelWithTooltip";
import { Select, SelectReadOnlyContext } from "./Select";

export interface SelectFieldProps<Value, Multiple extends boolean | undefined = false> extends SelectPrimitive.Root
  .Props<Value, Multiple> {
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  children: React.ReactNode;
}

export function SelectField<Value, Multiple extends boolean | undefined = false>({
  label,
  description,
  errorMessage,
  tooltip,
  className,
  name,
  disabled,
  readOnly,
  children,
  onValueChange,
  ...props
}: Readonly<SelectFieldProps<Value, Multiple>>) {
  const formErrors = useContext(FormValidationContext);
  const { errors, errorMessages, clearNow } = useFieldError({ name, errorMessage });

  const handleValueChange: typeof onValueChange = (value, event) => {
    clearNow();
    onValueChange?.(value, event);
  };

  // Merge the resolved errors back into the form context so SelectTrigger can show aria-invalid.
  // Using `errorMessages` (rather than the raw context) ensures this respects suppression.
  const triggerErrors = name && errorMessages.length > 0 ? { ...formErrors, [name]: errorMessages } : formErrors;

  return (
    <Field className={cn("flex flex-col", className)}>
      {label && (
        <Label htmlFor={disabled ? undefined : name} data-slot="field-label" className="cursor-default leading-snug">
          {tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label}
        </Label>
      )}
      <FormValidationContext.Provider value={triggerErrors}>
        <SelectReadOnlyContext.Provider value={!!readOnly}>
          <Select name={name} disabled={disabled} readOnly={readOnly} onValueChange={handleValueChange} {...props}>
            {children}
          </Select>
        </SelectReadOnlyContext.Provider>
      </FormValidationContext.Provider>
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

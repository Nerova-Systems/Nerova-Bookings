import type { Checkbox as CheckboxPrimitive } from "@base-ui/react/checkbox";

import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Checkbox } from "./Checkbox";
import { Field, FieldError } from "./Field";
import { Label } from "./Label";
import { LabelWithTooltip } from "./LabelWithTooltip";

export interface CheckboxFieldProps extends CheckboxPrimitive.Root.Props {
  label?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  readOnly?: boolean;
  alignWithLabel?: boolean;
}

export function CheckboxField({
  label,
  errorMessage,
  tooltip,
  className,
  name,
  readOnly,
  alignWithLabel,
  disabled,
  onCheckedChange,
  ...props
}: Readonly<CheckboxFieldProps>) {
  const { errors, clearNow } = useFieldError({ name, errorMessage });

  const handleCheckedChange: typeof onCheckedChange = (checked, event) => {
    clearNow();
    onCheckedChange?.(checked, event);
  };

  return (
    // alignWithLabel offset = FieldLabel height (0.875rem text-sm * 1.375 leading-snug = 1.203125rem) + Field gap-3 (0.75rem) = 1.953125rem.
    // Pushes a label-less checkbox down by exactly a label+gap, so it lines up with the input of a sibling field.
    <Field inline className={cn("flex-col gap-1", alignWithLabel && "mt-[1.953rem]", className)}>
      <Label className="min-h-(--control-height) leading-snug">
        <Checkbox
          name={name}
          disabled={disabled}
          onCheckedChange={readOnly ? undefined : handleCheckedChange}
          // In read-only mode, clicking the wrapping <Label> can't toggle the state, so we surface a focus ring via `focus:`
          // (not `focus-visible:`) as click feedback. Editable mode relies on the Checkbox's built-in `focus-visible:` so
          // mouse clicks don't leave a lingering ring -- the state change is feedback enough.
          className={readOnly ? "focus:outline-2 focus:outline-offset-2" : undefined}
          {...props}
        />
        {label && (tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label)}
      </Label>
      <FieldError errors={errors} />
    </Field>
  );
}

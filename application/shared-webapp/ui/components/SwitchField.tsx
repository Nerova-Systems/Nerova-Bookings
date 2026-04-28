import type { Switch as SwitchPrimitive } from "@base-ui/react/switch";

import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Field, FieldError } from "./Field";
import { Label } from "./Label";
import { LabelWithTooltip } from "./LabelWithTooltip";
import { Switch } from "./Switch";

export interface SwitchFieldProps extends SwitchPrimitive.Root.Props {
  label?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  readOnly?: boolean;
  alignWithLabel?: boolean;
}

export function SwitchField({
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
}: Readonly<SwitchFieldProps>) {
  const { errors, clearNow } = useFieldError({ name, errorMessage });

  const handleCheckedChange: typeof onCheckedChange = (checked, event) => {
    clearNow();
    onCheckedChange?.(checked, event);
  };

  return (
    // alignWithLabel offset = FieldLabel height (0.875rem text-sm * 1.375 leading-snug = 1.203125rem) + Field gap-3 (0.75rem) = 1.953125rem.
    // Pushes a label-less switch down by exactly a label+gap, so it lines up with the input of a sibling field.
    <Field inline className={cn("flex-col gap-1", alignWithLabel && "mt-[1.953rem]", className)}>
      <Label className="min-h-(--control-height) leading-snug">
        <Switch
          name={name}
          disabled={disabled}
          readOnly={readOnly}
          onCheckedChange={readOnly ? undefined : handleCheckedChange}
          {...props}
        />
        {label && (tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label)}
      </Label>
      <FieldError errors={errors} />
    </Field>
  );
}

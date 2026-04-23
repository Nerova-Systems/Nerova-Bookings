import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Field, FieldDescription, FieldError, FieldLabel } from "./Field";
import { LabelWithTooltip } from "./LabelWithTooltip";
import { Textarea } from "./Textarea";

export interface TextAreaFieldProps extends Omit<React.ComponentProps<"textarea">, "className" | "onChange"> {
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  textareaClassName?: string;
  onChange?: (value: string) => void;
  lines?: number;
  resizable?: boolean;
}

export function TextAreaField({
  label,
  description,
  errorMessage,
  tooltip,
  className,
  textareaClassName,
  name,
  value,
  onChange,
  autoFocus,
  required,
  disabled,
  readOnly,
  lines,
  resizable,
  ...props
}: Readonly<TextAreaFieldProps>) {
  const { errors, isInvalid, markChanged, clearOnBlur } = useFieldError({ name, errorMessage });

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    markChanged();
    onChange?.(e.target.value);
  };

  return (
    <Field className={cn("flex flex-col", className)}>
      {label && (
        <FieldLabel htmlFor={name}>
          {tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label}
        </FieldLabel>
      )}
      <Textarea
        id={name}
        name={name}
        value={value}
        onChange={handleChange}
        onBlur={clearOnBlur}
        autoFocus={autoFocus}
        required={required}
        disabled={disabled}
        readOnly={readOnly}
        aria-invalid={isInvalid || undefined}
        lines={lines}
        resizable={resizable}
        className={textareaClassName}
        {...props}
      />
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Field, FieldDescription, FieldError, FieldLabel } from "./Field";
import { Input } from "./Input";
import { InputGroup, InputGroupAddon, InputGroupInput } from "./InputGroup";
import { LabelWithTooltip } from "./LabelWithTooltip";

export interface TextFieldProps extends Omit<React.ComponentProps<"input">, "className" | "onChange"> {
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  inputClassName?: string;
  startIcon?: React.ReactNode;
  onChange?: (value: string) => void;
}

export function TextField({
  label,
  description,
  errorMessage,
  tooltip,
  className,
  inputClassName,
  startIcon,
  name,
  type,
  value,
  onChange,
  autoFocus,
  required,
  disabled,
  readOnly,
  ...props
}: Readonly<TextFieldProps>) {
  const { errors, isInvalid, markChanged, clearOnBlur } = useFieldError({ name, errorMessage });

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    markChanged();
    onChange?.(e.target.value);
  };

  const inputProps = {
    id: name,
    name,
    type,
    value,
    onChange: handleChange,
    onBlur: clearOnBlur,
    autoFocus,
    required,
    disabled,
    readOnly,
    "aria-invalid": isInvalid || undefined,
    ...props
  };

  return (
    <Field className={cn("flex flex-col", className)}>
      {label && (
        <FieldLabel htmlFor={name}>
          {tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label}
        </FieldLabel>
      )}
      {startIcon ? (
        <InputGroup data-disabled={disabled || undefined}>
          <InputGroupAddon>{startIcon}</InputGroupAddon>
          <InputGroupInput className={inputClassName} {...inputProps} />
        </InputGroup>
      ) : (
        <Input className={inputClassName} {...inputProps} />
      )}
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

import { REGEXP_ONLY_DIGITS } from "input-otp";

import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Field, FieldDescription, FieldError, FieldLabel } from "./Field";
import { InputOtp, InputOtpGroup, InputOtpSlot } from "./InputOtp";
import { LabelWithTooltip } from "./LabelWithTooltip";

export interface InputOtpFieldProps {
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  slotClassName?: string;
  name?: string;
  value?: string;
  onChange?: (value: string) => void;
  maxLength?: number;
  pattern?: string;
  autoComplete?: string;
  autoFocus?: boolean;
  disabled?: boolean;
  readOnly?: boolean;
  required?: boolean;
  "aria-label"?: string;
}

export function InputOtpField({
  label,
  description,
  errorMessage,
  tooltip,
  className,
  slotClassName,
  name,
  value,
  onChange,
  maxLength = 6,
  pattern = REGEXP_ONLY_DIGITS,
  autoComplete = "one-time-code",
  autoFocus,
  disabled,
  readOnly,
  required,
  "aria-label": ariaLabel
}: Readonly<InputOtpFieldProps>) {
  const { errors, isInvalid, markChanged, clearOnBlur } = useFieldError({ name, errorMessage });

  const handleChange = (nextValue: string) => {
    markChanged();
    onChange?.(nextValue);
  };

  return (
    <Field className={cn("flex flex-col", className)}>
      {label && (
        <FieldLabel htmlFor={name}>
          {tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label}
        </FieldLabel>
      )}
      <InputOtp
        id={name}
        name={name}
        maxLength={maxLength}
        value={value}
        onChange={handleChange}
        onBlur={clearOnBlur}
        disabled={disabled}
        readOnly={readOnly}
        required={required}
        autoFocus={autoFocus}
        pattern={pattern}
        autoComplete={autoComplete}
        aria-invalid={isInvalid || undefined}
        aria-label={ariaLabel}
      >
        <InputOtpGroup>
          {Array.from({ length: maxLength }).map((_, index) => (
            <InputOtpSlot key={index} index={index} className={slotClassName} aria-invalid={isInvalid || undefined} />
          ))}
        </InputOtpGroup>
      </InputOtp>
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

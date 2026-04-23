import { t } from "@lingui/core/macro";
import { CalendarIcon, XIcon } from "lucide-react";
import { type ReactNode } from "react";

import { type DateFieldDisplayFormat, useDateField } from "../hooks/useDateField";
import { cn } from "../utils";
import { Button } from "./Button";
import { Field, FieldDescription, FieldError } from "./Field";
import { Input } from "./Input";
import { Label } from "./Label";
import { LabelWithTooltip } from "./LabelWithTooltip";

export interface DateInputProps {
  id?: string;
  name?: string;
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  value?: string;
  onChange?: (value: string) => void;
  placeholder?: string;
  startIcon?: ReactNode;
  required?: boolean;
  disabled?: boolean;
  readOnly?: boolean;
  max?: string;
  min?: string;
  locale?: string;
  displayFormat?: DateFieldDisplayFormat;
}

// Standalone editable date field. Same masking, validation, and display-format options as
// DatePicker -- just without the calendar popover. Use it when you only want keyboard entry.
export function DateInput({
  id,
  name,
  label,
  description,
  errorMessage,
  tooltip,
  className,
  value,
  onChange,
  placeholder,
  startIcon = <CalendarIcon className="size-4 shrink-0" />,
  disabled,
  readOnly,
  max,
  min,
  locale,
  displayFormat = "input"
}: Readonly<DateInputProps>) {
  const {
    inputRef,
    inputFormat,
    isEditing,
    hasValue,
    inputValue,
    errors,
    isInvalid,
    handleInputChange,
    handleInputMouseDown,
    handleInputFocus,
    handleInputBlur,
    handleInputKeyDown,
    handleClear
  } = useDateField({ value, onChange, name, errorMessage, min, max, locale, displayFormat });

  const triggerId = id ?? name;
  const showTrailingControls = !readOnly && !disabled;

  return (
    <Field className={cn("flex flex-col", className)}>
      {label && (
        <Label
          htmlFor={disabled ? undefined : triggerId}
          data-slot="field-label"
          className="cursor-default leading-snug"
        >
          {tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label}
        </Label>
      )}
      {name && <input type="hidden" name={name} value={value ?? ""} />}
      <div className="group relative">
        <Input
          ref={inputRef}
          id={triggerId}
          aria-invalid={isInvalid || undefined}
          placeholder={isEditing ? inputFormat.toLowerCase() : placeholder}
          value={inputValue}
          onChange={handleInputChange}
          onMouseDown={handleInputMouseDown}
          onFocus={handleInputFocus}
          onBlur={handleInputBlur}
          onKeyDown={handleInputKeyDown}
          disabled={disabled}
          readOnly={readOnly}
          autoComplete="off"
          inputMode={isEditing ? "numeric" : undefined}
          className={cn(
            startIcon != null && "pl-9",
            showTrailingControls && hasValue && (isEditing ? "pr-9" : "group-hover:pr-9")
          )}
        />
        {startIcon != null && (
          <div
            className={cn(
              "pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2",
              hasValue ? "text-foreground" : "text-muted-foreground"
            )}
          >
            {startIcon}
          </div>
        )}
        {showTrailingControls && hasValue && (
          <Button
            variant="ghost"
            size="icon-xs"
            tabIndex={-1}
            className={cn(
              "absolute top-1/2 right-1 -translate-y-1/2 transition-opacity",
              isEditing ? "opacity-100" : "opacity-0 group-hover:opacity-100"
            )}
            onClick={handleClear}
            aria-label={t`Clear date`}
          >
            <XIcon className="size-5" />
          </Button>
        )}
      </div>
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

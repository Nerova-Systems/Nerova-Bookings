import { t } from "@lingui/core/macro";
import { CalendarIcon, XIcon } from "lucide-react";
import { type ReactNode, useRef, useState } from "react";

import { type DateFieldDisplayFormat, useDateField } from "../hooks/useDateField";
import { cn } from "../utils";
import { Button } from "./Button";
import { Calendar } from "./Calendar";
import { Field, FieldDescription, FieldError } from "./Field";
import { Input } from "./Input";
import { Label } from "./Label";
import { LabelWithTooltip } from "./LabelWithTooltip";
import { Popover, PopoverContent } from "./Popover";

export interface DatePickerProps {
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
  showDropdowns?: boolean;
  locale?: string;
  // Optional per-date predicate -- return true to disable the date in the calendar (e.g. weekends).
  // Combined with min/max so consumers can mix range limits with arbitrary exclusions.
  disabledDate?: (date: Date) => boolean;
  // How to render the value when the input isn't focused. Defaults to "input" so the resting
  // display matches the editable format. Built-in presets: "short" (Apr 19, 2026), "long" (April
  // 19th, 2026), "relative" (Today / Yesterday / In 3 days). Anything else is treated as a
  // date-fns format string.
  displayFormat?: DateFieldDisplayFormat;
}

export function DatePicker({
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
  showDropdowns,
  locale,
  disabledDate,
  displayFormat = "input"
}: Readonly<DatePickerProps>) {
  const [open, setOpen] = useState(false);
  const {
    inputRef,
    inputFormat,
    isEditing,
    hasValue,
    inputValue,
    selectedDate,
    previewDate,
    maxDate,
    minDate,
    errors,
    isInvalid,
    handleInputChange,
    handleInputMouseDown: baseHandleMouseDown,
    handleInputFocus,
    handleInputBlur: baseHandleBlur,
    handleInputKeyDown: baseHandleKeyDown,
    handleClear,
    commitDate
  } = useDateField({
    value,
    onChange,
    name,
    errorMessage,
    min,
    max,
    locale,
    displayFormat,
    externalEditingOverride: open
  });

  const triggerId = id ?? name;
  const showTrailingControls = !readOnly && !disabled;
  const calendarMonth = previewDate ?? selectedDate;

  // Track whether the Calendar should steal focus on open. Mouse opens keep focus on the input so
  // the user can keep typing; keyboard opens move focus into the calendar so arrow keys navigate.
  const openedByKeyboardRef = useRef(false);
  // Captured at mousedown -- the click handler uses these to decide whether the click should open
  // the popover or just position the caret / let BaseUI close the popover.
  const wasFocusedBeforeClickRef = useRef(false);
  const wasOpenAtMouseDownRef = useRef(false);

  const handleInputMouseDown = (event: React.MouseEvent) => {
    baseHandleMouseDown();
    wasFocusedBeforeClickRef.current = inputRef.current === document.activeElement;
    wasOpenAtMouseDownRef.current = open;
    // Suppress native focus-on-click on the first click into an unfocused, enabled input. Without
    // this the input is focused for the brief gap between mousedown and the popover render, so the
    // focus ring flashes on and then disappears once the calendar takes over the visual hierarchy.
    // We re-focus the input ourselves in handleInputClick so the user can keep typing.
    if (!disabled && !readOnly && !open && !wasFocusedBeforeClickRef.current) {
      event.preventDefault();
    }
  };

  const handleInputClick = () => {
    if (readOnly || disabled) {
      return;
    }
    if (wasOpenAtMouseDownRef.current) {
      // BaseUI's outside-click dismiss is closing the popover as part of this same click.
      return;
    }
    if (wasFocusedBeforeClickRef.current) {
      // Click on an already-focused input -- caret positioning, not a popover toggle.
      return;
    }
    openedByKeyboardRef.current = false;
    setOpen(true);
    // mousedown preventDefault skipped the native focus, so move focus programmatically here. The
    // browser treats this as mouse-modality focus, so :focus-visible stays off and no ring appears.
    inputRef.current?.focus();
  };

  const handleInputBlur = () => {
    baseHandleBlur();
    // Reset interaction-state refs so a label click (which fires click without mousedown) doesn't
    // inherit stale values and suppress the open.
    wasFocusedBeforeClickRef.current = false;
    wasOpenAtMouseDownRef.current = false;
  };

  const handleInputKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown" && !open) {
      event.preventDefault();
      openedByKeyboardRef.current = true;
      setOpen(true);
      return;
    }
    baseHandleKeyDown(event);
  };

  const handleCalendarSelect = (date: Date | undefined) => {
    if (date) {
      commitDate(date);
      setOpen(false);
      inputRef.current?.focus();
    }
  };

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
        <Popover open={readOnly ? false : open} onOpenChange={readOnly ? () => {} : setOpen}>
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
            onClick={handleInputClick}
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
            <button
              type="button"
              tabIndex={-1}
              disabled={readOnly || disabled}
              aria-label={t`Open calendar`}
              // mousedown preventDefault keeps the input focused when the icon is clicked so the
              // input doesn't blur (and commit) just to open the calendar.
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => {
                if (readOnly || disabled) {
                  return;
                }
                openedByKeyboardRef.current = false;
                setOpen(true);
              }}
              className={cn(
                "absolute top-1/2 left-2.5 -translate-y-1/2 cursor-pointer rounded outline-ring focus-visible:outline-2 focus-visible:outline-offset-2",
                hasValue ? "text-foreground" : "text-muted-foreground",
                (readOnly || disabled) && "pointer-events-none cursor-default"
              )}
            >
              {startIcon}
            </button>
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
          <PopoverContent
            className="w-auto overflow-hidden p-0"
            align="start"
            anchor={() => inputRef.current}
            onKeyDown={(event: React.KeyboardEvent) => {
              if (event.key === "Tab") {
                event.preventDefault();
                setOpen(false);
                inputRef.current?.focus();
              }
            }}
          >
            <Calendar
              autoFocus={openedByKeyboardRef.current}
              mode="single"
              selected={selectedDate}
              onSelect={handleCalendarSelect}
              defaultMonth={calendarMonth}
              numberOfMonths={1}
              startMonth={minDate ?? (showDropdowns ? new Date(1900, 0) : undefined)}
              endMonth={maxDate ?? (showDropdowns ? new Date() : undefined)}
              {...(showDropdowns && { captionLayout: "dropdown" as const })}
              disabledDates={(date) => {
                if (maxDate && date > maxDate) {
                  return true;
                }
                if (minDate && date < minDate) {
                  return true;
                }
                return disabledDate?.(date) ?? false;
              }}
            />
          </PopoverContent>
        </Popover>
      </div>
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

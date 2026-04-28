import { t } from "@lingui/core/macro";
import { CalendarIcon, XIcon } from "lucide-react";
import { type ReactNode, useRef, useState } from "react";

import { type DateFieldDisplayFormat } from "../hooks/useDateField";
import { type DateRangeValue, useDateRangeField } from "../hooks/useDateRangeField";
import { cn } from "../utils";
import { Button } from "./Button";
import { Calendar } from "./Calendar";
import { Field, FieldDescription, FieldError } from "./Field";
import { Input } from "./Input";
import { Label } from "./Label";
import { LabelWithTooltip } from "./LabelWithTooltip";
import { Popover, PopoverContent } from "./Popover";

export type { DateRangeValue };

export interface DateRangePickerProps {
  id?: string;
  name?: string;
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  className?: string;
  value?: DateRangeValue | null;
  onChange?: (value: DateRangeValue | null) => void;
  placeholder?: string;
  startIcon?: ReactNode;
  required?: boolean;
  disabled?: boolean;
  readOnly?: boolean;
  locale?: string;
  displayFormat?: DateFieldDisplayFormat;
}

export function DateRangePicker({
  id,
  name,
  label,
  description,
  errorMessage,
  tooltip,
  className,
  value,
  onChange,
  placeholder = t`Select dates`,
  startIcon = <CalendarIcon className="size-4 shrink-0" />,
  disabled,
  readOnly,
  locale,
  displayFormat = "input"
}: Readonly<DateRangePickerProps>) {
  const [open, setOpen] = useState(false);
  const [selectionsCount, setSelectionsCount] = useState(0);
  const [firstClickDate, setFirstClickDate] = useState<Date | null>(null);
  const {
    inputRef,
    rangeInputFormat,
    isEditing,
    hasValue,
    inputValue,
    selectedRange,
    previewDate,
    errors,
    isInvalid,
    handleInputChange,
    handleInputMouseDown: baseHandleMouseDown,
    handleInputFocus,
    handleInputBlur: baseHandleBlur,
    handleInputKeyDown: baseHandleKeyDown,
    handleClear,
    commitRange
  } = useDateRangeField({
    value,
    onChange,
    name,
    errorMessage,
    locale,
    displayFormat,
    externalEditingOverride: open
  });

  const triggerId = id ?? name;
  const showTrailingControls = !readOnly && !disabled;
  const calendarMonth = previewDate ?? value?.start ?? value?.end;

  const openedByKeyboardRef = useRef(false);
  const wasFocusedBeforeClickRef = useRef(false);
  const wasOpenAtMouseDownRef = useRef(false);

  const handleOpenChange = (isOpen: boolean) => {
    setOpen(isOpen);
    if (isOpen) {
      setSelectionsCount(0);
      setFirstClickDate(null);
    }
  };

  const handleInputMouseDown = (event: React.MouseEvent) => {
    baseHandleMouseDown();
    wasFocusedBeforeClickRef.current = inputRef.current === document.activeElement;
    wasOpenAtMouseDownRef.current = open;
    if (!disabled && !readOnly && !open && !wasFocusedBeforeClickRef.current) {
      event.preventDefault();
    }
  };

  const handleInputClick = () => {
    if (readOnly || disabled) {
      return;
    }
    if (wasOpenAtMouseDownRef.current) {
      return;
    }
    if (wasFocusedBeforeClickRef.current) {
      return;
    }
    openedByKeyboardRef.current = false;
    handleOpenChange(true);
    inputRef.current?.focus();
  };

  const handleInputBlur = () => {
    baseHandleBlur();
    wasFocusedBeforeClickRef.current = false;
    wasOpenAtMouseDownRef.current = false;
  };

  const handleInputKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown" && !open) {
      event.preventDefault();
      openedByKeyboardRef.current = true;
      handleOpenChange(true);
      return;
    }
    baseHandleKeyDown(event);
  };

  const handleDayClick = (day: Date) => {
    const newCount = selectionsCount + 1;
    setSelectionsCount(newCount);

    if (newCount === 1) {
      // First click pivots on the existing range: clicking before the start becomes the new start,
      // clicking on or after the start becomes the new end. With no existing range the day becomes
      // a single-day range that the next click expands.
      const existingStart = value?.start;
      const existingEnd = value?.end;
      setFirstClickDate(day);

      if (existingStart && existingEnd) {
        if (day.getTime() < existingStart.getTime()) {
          commitRange({ start: day, end: existingEnd });
        } else {
          commitRange({ start: existingStart, end: day });
        }
      } else {
        commitRange({ start: day, end: day });
      }
      return;
    }
    // Second click: pair with the first click; swap so earlier becomes start.
    const firstDate = firstClickDate ?? day;
    let newStart = firstDate;
    let newEnd = day;
    if (newStart.getTime() > newEnd.getTime()) {
      [newStart, newEnd] = [newEnd, newStart];
    }
    commitRange({ start: newStart, end: newEnd });
    if (newStart.getTime() !== newEnd.getTime()) {
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
      <div className="group relative">
        <Popover open={readOnly ? false : open} onOpenChange={readOnly ? () => {} : handleOpenChange}>
          <Input
            ref={inputRef}
            id={triggerId}
            aria-invalid={isInvalid || undefined}
            placeholder={isEditing ? rangeInputFormat.toLowerCase() : placeholder}
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
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => {
                if (readOnly || disabled) {
                  return;
                }
                openedByKeyboardRef.current = false;
                handleOpenChange(true);
              }}
              className={cn(
                "absolute top-1/2 left-2.5 -translate-y-1/2 cursor-pointer rounded outline-ring focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
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
              aria-label={t`Clear dates`}
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
              mode="range"
              selected={selectedRange}
              onDayClick={handleDayClick}
              defaultMonth={calendarMonth}
              numberOfMonths={1}
            />
          </PopoverContent>
        </Popover>
      </div>
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}

import type { DateRange } from "react-day-picker";

import { format, isValid, parse } from "date-fns";
import { enUS } from "date-fns/locale";
import { useContext, useEffect, useRef, useState } from "react";

import type { DateFieldDisplayFormat } from "./useDateField";

import { resolveInputFormat } from "../utils/dateInputFormat";
import {
  dateFnsLocaleMap,
  expandShortYear,
  isValidPartialDate,
  maskDateInput,
  parseIsoDate,
  toIsoDateString,
  toParseFormat
} from "./dateFieldInternals";
import { translationContext } from "./translationContext";
import { useFieldError } from "./useFieldError";
import { useFormatDate, useFormatLongDate, useFormatRelativeDate } from "./useSmartDate";

export const RANGE_SEPARATOR = " - ";

export interface DateRangeValue {
  start: Date;
  end: Date;
}

export interface UseDateRangeFieldOptions {
  value?: DateRangeValue | null;
  onChange?: (value: DateRangeValue | null) => void;
  name?: string;
  errorMessage?: string;
  min?: string;
  max?: string;
  locale?: string;
  displayFormat?: DateFieldDisplayFormat;
  externalEditingOverride?: boolean;
}

function splitRange(text: string): { startText: string; endText: string; separatorIndex: number } {
  const separatorIndex = text.indexOf(RANGE_SEPARATOR);
  if (separatorIndex === -1) {
    return { startText: text, endText: "", separatorIndex: -1 };
  }
  return {
    startText: text.slice(0, separatorIndex),
    endText: text.slice(separatorIndex + RANGE_SEPARATOR.length),
    separatorIndex
  };
}

function maskDateRangeInput(text: string, inputFormat: string): string {
  const { startText, endText, separatorIndex } = splitRange(text);
  if (separatorIndex === -1) {
    const masked = maskDateInput(text, inputFormat);
    // When the start segment is complete, auto-insert the range separator so typing continues
    // straight into the end segment.
    return masked.length === inputFormat.length ? masked + RANGE_SEPARATOR : masked;
  }
  return maskDateInput(startText, inputFormat) + RANGE_SEPARATOR + maskDateInput(endText, inputFormat);
}

function isValidPartialRange(text: string, inputFormat: string): boolean {
  const { startText, endText, separatorIndex } = splitRange(text);
  if (separatorIndex === -1) {
    return isValidPartialDate(text, inputFormat);
  }
  return isValidPartialDate(startText, inputFormat) && isValidPartialDate(endText, inputFormat);
}

export function useDateRangeField({
  value,
  onChange,
  name,
  errorMessage,
  min,
  max,
  locale,
  displayFormat = "input",
  externalEditingOverride = false
}: UseDateRangeFieldOptions) {
  const { currentLocale } = useContext(translationContext);
  const resolvedLocale = locale ?? currentLocale ?? "en-US";
  const dateLocale = dateFnsLocaleMap[resolvedLocale] ?? enUS;
  const inputFormat = resolveInputFormat(resolvedLocale);
  const rangeInputFormat = inputFormat + RANGE_SEPARATOR + inputFormat;

  const { errors, isInvalid, markChanged, clearOnBlur, clearNow } = useFieldError({ name, errorMessage });

  const maxDate = parseIsoDate(max);
  const minDate = parseIsoDate(min);

  const formatShortDate = useFormatDate();
  const formatLongDate = useFormatLongDate();
  const formatRelativeDate = useFormatRelativeDate();

  const safeFormat = (date: Date | undefined, pattern: string) =>
    date && isValid(date) ? format(date, pattern, { locale: dateLocale }) : "";
  const formatForInput = (date: Date | undefined) => safeFormat(date, inputFormat);
  const formatForDisplay = (date: Date | undefined): string => {
    if (!date || !isValid(date)) {
      return "";
    }
    const isoLocal = `${toIsoDateString(date)}T00:00:00`;
    if (displayFormat === "relative") {
      return formatRelativeDate(isoLocal);
    }
    if (displayFormat === "short") {
      return formatShortDate(isoLocal);
    }
    if (displayFormat === "long") {
      return formatLongDate(isoLocal);
    }
    const pattern = displayFormat === "input" ? inputFormat : displayFormat;
    return safeFormat(date, pattern);
  };
  const formatRangeForInput = (v: DateRangeValue): string =>
    formatForInput(v.start) + RANGE_SEPARATOR + formatForInput(v.end);
  const formatRangeForDisplay = (v: DateRangeValue | null | undefined): string =>
    v ? formatForDisplay(v.start) + RANGE_SEPARATOR + formatForDisplay(v.end) : "";

  const [editingText, setEditingText] = useState<string>(() => (value ? formatRangeForInput(value) : ""));
  const [isFocused, setIsFocused] = useState(false);
  const [activeEndpoint, setActiveEndpoint] = useState<"start" | "end">("start");
  const inputRef = useRef<HTMLInputElement>(null);
  const placeCursorAtEndOnNextFocusRef = useRef(false);
  const focusedByClickRef = useRef(false);

  useEffect(() => {
    setEditingText(value ? formatRangeForInput(value) : "");
    // formatRangeForInput closes over inputFormat and dateLocale; both derived from resolvedLocale.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, resolvedLocale]);

  const hasValue = value !== null && value !== undefined;
  const isEditing = isFocused || externalEditingOverride;
  const selectedRange: DateRange | undefined = value ? { from: value.start, to: value.end } : undefined;
  const inputValue = isEditing ? editingText : formatRangeForDisplay(value);

  const parseSegment = (text: string): Date | undefined => {
    const trimmed = text.trim();
    if (!trimmed) {
      return undefined;
    }
    const parsed = parse(expandShortYear(trimmed, inputFormat), toParseFormat(inputFormat), new Date(), {
      locale: dateLocale
    });
    return isValid(parsed) ? parsed : undefined;
  };

  const { startText: previewStartText, endText: previewEndText } = splitRange(editingText);
  const startPreview = parseSegment(previewStartText);
  const endPreview = parseSegment(previewEndText);
  const previewDate = activeEndpoint === "end" ? (endPreview ?? startPreview) : (startPreview ?? endPreview);

  const computeEndpoint = (caret: number): "start" | "end" => {
    const separatorIndex = editingText.indexOf(RANGE_SEPARATOR);
    return separatorIndex !== -1 && caret >= separatorIndex + RANGE_SEPARATOR.length ? "end" : "start";
  };

  const commitInputText = () => {
    const text = editingText.trim();
    if (text === "") {
      if (hasValue) {
        clearNow();
        onChange?.(null);
      }
      return;
    }
    const { startText, endText, separatorIndex } = splitRange(text);
    if (separatorIndex === -1 || startText.trim() === "" || endText.trim() === "") {
      // Only one endpoint filled in -- revert to the committed value instead of fabricating a range.
      setEditingText(value ? formatRangeForInput(value) : "");
      return;
    }
    const parsedStart = parseSegment(startText);
    const parsedEnd = parseSegment(endText);
    if (!parsedStart || !parsedEnd) {
      setEditingText(value ? formatRangeForInput(value) : "");
      return;
    }
    const isOutOfRange =
      (maxDate && (parsedStart > maxDate || parsedEnd > maxDate)) ||
      (minDate && (parsedStart < minDate || parsedEnd < minDate));
    if (isOutOfRange) {
      setEditingText("");
      if (hasValue) {
        clearNow();
        onChange?.(null);
      }
      return;
    }
    let start = parsedStart;
    let end = parsedEnd;
    if (start.getTime() > end.getTime()) {
      [start, end] = [end, start];
    }
    const unchanged =
      value &&
      toIsoDateString(start) === toIsoDateString(value.start) &&
      toIsoDateString(end) === toIsoDateString(value.end);
    if (unchanged) {
      setEditingText(formatRangeForInput({ start, end }));
    } else {
      clearNow();
      onChange?.({ start, end });
    }
  };

  const handleInputChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const input = event.currentTarget;
    const nextValue = input.value;
    if (nextValue !== "" && !isValidPartialRange(nextValue, inputFormat)) {
      const insertedLength = Math.max(0, nextValue.length - editingText.length);
      const restorePosition = Math.max(0, (input.selectionStart ?? 0) - insertedLength);
      requestAnimationFrame(() => {
        input.setSelectionRange(restorePosition, restorePosition);
      });
      return;
    }
    markChanged();
    const isAppendAtEnd = nextValue.length > editingText.length && nextValue.startsWith(editingText);
    setEditingText(isAppendAtEnd ? maskDateRangeInput(nextValue, inputFormat) : nextValue);
    setActiveEndpoint(computeEndpoint(input.selectionStart ?? nextValue.length));
  };

  const handleInputMouseDown = () => {
    focusedByClickRef.current = true;
  };

  const handleInputFocus = () => {
    setIsFocused(true);
    if (placeCursorAtEndOnNextFocusRef.current) {
      placeCursorAtEndOnNextFocusRef.current = false;
      requestAnimationFrame(() => {
        const input = inputRef.current;
        if (input) {
          const cursorPosition = input.value.length;
          input.setSelectionRange(cursorPosition, cursorPosition);
        }
      });
      return;
    }
    if (focusedByClickRef.current) {
      focusedByClickRef.current = false;
      return;
    }
    requestAnimationFrame(() => inputRef.current?.select());
  };

  const handleInputBlur = () => {
    setIsFocused(false);
    clearOnBlur();
    commitInputText();
  };

  const handleInputKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    const input = event.currentTarget;
    if (event.key === "Enter") {
      event.preventDefault();
      commitInputText();
      return;
    }
    const caret = input.selectionStart ?? 0;
    const hasSelection = caret !== input.selectionEnd;
    // Atomic separator deletion: backspace at the right edge of " - ", Delete at the left edge.
    if (!hasSelection && event.key === "Backspace" && caret >= RANGE_SEPARATOR.length) {
      const before = editingText.slice(caret - RANGE_SEPARATOR.length, caret);
      if (before === RANGE_SEPARATOR) {
        event.preventDefault();
        const nextText = editingText.slice(0, caret - RANGE_SEPARATOR.length) + editingText.slice(caret);
        setEditingText(nextText);
        const nextCaret = caret - RANGE_SEPARATOR.length;
        requestAnimationFrame(() => input.setSelectionRange(nextCaret, nextCaret));
        setActiveEndpoint(computeEndpoint(nextCaret));
        markChanged();
        return;
      }
    }
    if (!hasSelection && event.key === "Delete") {
      const after = editingText.slice(caret, caret + RANGE_SEPARATOR.length);
      if (after === RANGE_SEPARATOR) {
        event.preventDefault();
        const nextText = editingText.slice(0, caret) + editingText.slice(caret + RANGE_SEPARATOR.length);
        setEditingText(nextText);
        requestAnimationFrame(() => input.setSelectionRange(caret, caret));
        setActiveEndpoint(computeEndpoint(caret));
        markChanged();
        return;
      }
    }
    // Hyphen typed against an unfinished short-year start segment ("02-12-26-") is a request for
    // the range separator. Auto-pad the year, then insert " - " and jump the caret into the end
    // segment. Only fires when the start segment parses as a valid date (with short-year expansion)
    // and the range separator hasn't been inserted yet.
    if (!hasSelection && event.key === "-" && editingText.indexOf(RANGE_SEPARATOR) === -1) {
      const trimmed = editingText.trim();
      if (trimmed) {
        const expanded = expandShortYear(trimmed, inputFormat);
        const parsed = parse(expanded, toParseFormat(inputFormat), new Date(), { locale: dateLocale });
        if (isValid(parsed)) {
          event.preventDefault();
          const nextText = format(parsed, inputFormat, { locale: dateLocale }) + RANGE_SEPARATOR;
          setEditingText(nextText);
          const nextCaret = nextText.length;
          requestAnimationFrame(() => input.setSelectionRange(nextCaret, nextCaret));
          setActiveEndpoint("end");
          markChanged();
          return;
        }
      }
    }
    // Any other key: update activeEndpoint after the caret settles.
    requestAnimationFrame(() => {
      if (inputRef.current) {
        setActiveEndpoint(computeEndpoint(inputRef.current.selectionStart ?? 0));
      }
    });
  };

  const handleClear = (event: React.MouseEvent) => {
    event.preventDefault();
    event.stopPropagation();
    clearNow();
    onChange?.(null);
    inputRef.current?.focus();
  };

  const commitRange = (range: DateRangeValue) => {
    clearNow();
    onChange?.(range);
    placeCursorAtEndOnNextFocusRef.current = true;
  };

  return {
    inputRef,
    inputFormat,
    rangeInputFormat,
    dateLocale,
    resolvedLocale,
    editingText,
    setEditingText,
    isFocused,
    isEditing,
    hasValue,
    inputValue,
    selectedRange,
    previewDate,
    activeEndpoint,
    maxDate,
    minDate,
    errors,
    isInvalid,
    handleInputChange,
    handleInputMouseDown,
    handleInputFocus,
    handleInputBlur,
    handleInputKeyDown,
    handleClear,
    commitInputText,
    commitRange,
    markChanged
  };
}

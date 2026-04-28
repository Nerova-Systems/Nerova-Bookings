import { format, isValid, parse } from "date-fns";
import { enUS } from "date-fns/locale";
import { useContext, useEffect, useRef, useState } from "react";

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

export type DateFieldDisplayFormat = "input" | "short" | "long" | "relative" | (string & {});

export interface UseDateFieldOptions {
  value?: string;
  onChange?: (value: string) => void;
  name?: string;
  errorMessage?: string;
  min?: string;
  max?: string;
  locale?: string;
  displayFormat?: DateFieldDisplayFormat;
  // When true the input stays in "editing" display mode even without DOM focus. DatePicker uses
  // this to keep the editable format visible while the calendar popover has focus.
  externalEditingOverride?: boolean;
}

export function useDateField({
  value,
  onChange,
  name,
  errorMessage,
  min,
  max,
  locale,
  displayFormat = "input",
  externalEditingOverride = false
}: UseDateFieldOptions) {
  const { currentLocale } = useContext(translationContext);
  const resolvedLocale = locale ?? currentLocale ?? "en-US";
  const dateLocale = dateFnsLocaleMap[resolvedLocale] ?? enUS;
  const inputFormat = resolveInputFormat(resolvedLocale);

  const { errors, isInvalid, markChanged, clearOnBlur, clearNow } = useFieldError({ name, errorMessage });

  const selectedDate = parseIsoDate(value);
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

  const [editingText, setEditingText] = useState<string>(() => formatForInput(selectedDate));
  const [isFocused, setIsFocused] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  // Flag set on blur and consumed on next focus to place the caret at the end of the value.
  const placeCursorAtEndOnNextFocusRef = useRef(false);
  // mousedown fires before focus, so a click sets this flag and the focus handler skips select-all
  // (browser places the caret at the click position instead).
  const focusedByClickRef = useRef(false);

  useEffect(() => {
    setEditingText(formatForInput(selectedDate));
    // formatForInput closes over inputFormat and dateLocale; both are derived from resolvedLocale.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, resolvedLocale]);

  const hasValue = !!value;
  const isEditing = isFocused || externalEditingOverride;
  const displayText = formatForDisplay(selectedDate);
  const inputValue = isEditing ? editingText : displayText;

  const previewDate = (() => {
    const text = editingText.trim();
    if (!text) {
      return undefined;
    }
    const parsed = parse(expandShortYear(text, inputFormat), toParseFormat(inputFormat), new Date(), {
      locale: dateLocale
    });
    return isValid(parsed) ? parsed : undefined;
  })();

  const commitInputText = () => {
    const text = editingText.trim();
    if (text === "") {
      if (hasValue) {
        clearNow();
        onChange?.("");
      }
      return;
    }
    const parseFormat = toParseFormat(inputFormat);
    const reference = new Date();
    // Expand short years up front so parsing always sees a 4-digit year. date-fns would otherwise
    // consume "26" as year 26 and the ISO round-trip would break.
    const normalizedText = expandShortYear(text, inputFormat);
    const parsed = parse(normalizedText, parseFormat, reference, { locale: dateLocale });
    const isOutOfRange = !isValid(parsed) || (maxDate && parsed > maxDate) || (minDate && parsed < minDate);
    if (isOutOfRange) {
      // Date doesn't parse (e.g. 31/02/2026). Clear instead of reverting so the user sees the
      // entry was rejected without stale text being left behind.
      setEditingText("");
      if (hasValue) {
        clearNow();
        onChange?.("");
      }
      return;
    }
    const iso = toIsoDateString(parsed);
    if (iso !== value) {
      clearNow();
      onChange?.(iso);
    } else {
      setEditingText(formatForInput(parsed));
    }
  };

  const handleInputChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const input = event.currentTarget;
    const nextValue = input.value;
    if (nextValue !== "" && !isValidPartialDate(nextValue, inputFormat)) {
      // Reject: restore the caret to where it was before the insert so React's re-render doesn't
      // jump the cursor to the end.
      const insertedLength = Math.max(0, nextValue.length - editingText.length);
      const restorePosition = Math.max(0, (input.selectionStart ?? 0) - insertedLength);
      requestAnimationFrame(() => {
        input.setSelectionRange(restorePosition, restorePosition);
      });
      return;
    }
    markChanged();
    const isAppendAtEnd = nextValue.length > editingText.length && nextValue.startsWith(editingText);
    setEditingText(isAppendAtEnd ? maskDateInput(nextValue, inputFormat) : nextValue);
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

  const handleInputKeyDownDefault = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      // Commit the typed value without submitting the enclosing form; React state updates would
      // otherwise race the form's submit handler reading the hidden input.
      event.preventDefault();
      commitInputText();
    }
  };

  const handleClear = (event: React.MouseEvent) => {
    event.preventDefault();
    event.stopPropagation();
    clearNow();
    onChange?.("");
    inputRef.current?.focus();
  };

  const commitDate = (date: Date) => {
    clearNow();
    onChange?.(toIsoDateString(date));
    placeCursorAtEndOnNextFocusRef.current = true;
  };

  return {
    inputRef,
    inputFormat,
    dateLocale,
    resolvedLocale,
    editingText,
    setEditingText,
    isFocused,
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
    handleInputMouseDown,
    handleInputFocus,
    handleInputBlur,
    handleInputKeyDown: handleInputKeyDownDefault,
    handleClear,
    commitInputText,
    commitDate,
    placeCursorAtEndOnNextFocusRef,
    focusedByClickRef,
    markChanged
  };
}

export { toIsoDateString } from "./dateFieldInternals";

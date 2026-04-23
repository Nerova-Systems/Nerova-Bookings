import { type Locale } from "date-fns";
import { da, enUS } from "date-fns/locale";

export const dateFnsLocaleMap: Record<string, Locale> = {
  "en-US": enUS,
  "da-DK": da
};

export interface DateFieldSpecification {
  length: number;
  minimum: number;
  maximum: number;
}

// Field ranges keyed by date-fns format tokens. The mask only allows typing characters that could
// still lead to a value inside the range; e.g. "3" as the first day digit is accepted (could
// become 30 or 31), but "4" is rejected since no completion in dd would be <= 31.
export const dateFieldSpecifications: Record<string, DateFieldSpecification> = {
  d: { length: 2, minimum: 1, maximum: 31 },
  M: { length: 2, minimum: 1, maximum: 12 },
  y: { length: 4, minimum: 0, maximum: 9999 }
};

export function toIsoDateString(date: Date): string {
  const year = String(date.getFullYear()).padStart(4, "0");
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

// True if `digits` is itself in range OR could be completed to an in-range value with more digits.
export function isPartialInRange(digits: string, specification: DateFieldSpecification): boolean {
  const value = Number(digits);
  const remainingPlaces = 10 ** (specification.length - digits.length);
  const rangeLow = value * remainingPlaces;
  const rangeHigh = rangeLow + remainingPlaces - 1;
  const valueInRange = value >= specification.minimum && value <= specification.maximum;
  const completionInRange = rangeHigh >= specification.minimum && rangeLow <= specification.maximum;
  return valueInRange || completionInRange;
}

export function hasAnyValidNextDigit(digits: string, specification: DateFieldSpecification): boolean {
  for (let nextDigit = 0; nextDigit < 10; nextDigit++) {
    if (isPartialInRange(digits + nextDigit, specification)) {
      return true;
    }
  }
  return false;
}

// Applies the mask to text, auto-padding single-digit fields on separator and auto-inserting
// separators after a full field. Designed for sequential-append input (e.g. user typing at the end
// of the value); callers should avoid running the mask on mid-text edits.
export function maskDateInput(text: string, format: string): string {
  let output = "";
  let currentField = "";
  let fieldDigits = "";

  const finalizeField = (separatorPosition: number) => {
    if (separatorPosition < format.length && !dateFieldSpecifications[format[separatorPosition]]) {
      output += format[separatorPosition];
      currentField = "";
      fieldDigits = "";
    }
  };

  for (const inputCharacter of text) {
    const position = output.length;
    if (position >= format.length) {
      return output;
    }
    const formatCharacter = format[position];
    const specification = dateFieldSpecifications[formatCharacter];
    if (specification) {
      if (formatCharacter !== currentField) {
        currentField = formatCharacter;
        fieldDigits = "";
      }
      if (fieldDigits === "" && position > 0 && inputCharacter === output[position - 1]) {
        continue;
      }
      if (/\d/.test(inputCharacter)) {
        const nextDigits = fieldDigits + inputCharacter;
        const nextValue = Number(nextDigits);
        if (!isPartialInRange(nextDigits, specification)) {
          return output;
        }
        output += inputCharacter;
        fieldDigits = nextDigits;
        const currentInRange = nextValue >= specification.minimum && nextValue <= specification.maximum;
        const isFull = nextDigits.length === specification.length;
        const isImplicitlyDone = !isFull && currentInRange && !hasAnyValidNextDigit(nextDigits, specification);
        if (isFull || isImplicitlyDone) {
          if (nextDigits.length < specification.length) {
            const padded = nextDigits.padStart(specification.length, "0");
            output = output.slice(0, -nextDigits.length) + padded;
            fieldDigits = padded;
          }
          finalizeField(output.length);
        }
        continue;
      }
      const fieldEndPosition = position + (specification.length - fieldDigits.length);
      if (
        fieldEndPosition < format.length &&
        format[fieldEndPosition] === inputCharacter &&
        fieldDigits.length > 0 &&
        fieldDigits.length < specification.length
      ) {
        const partialValue = Number(fieldDigits);
        if (partialValue >= specification.minimum && partialValue <= specification.maximum) {
          const padded = fieldDigits.padStart(specification.length, "0");
          output = output.slice(0, -fieldDigits.length) + padded + inputCharacter;
          currentField = "";
          fieldDigits = "";
          continue;
        }
      }
      return output;
    }
    currentField = "";
    fieldDigits = "";
    if (inputCharacter !== formatCharacter) {
      return output;
    }
    output += inputCharacter;
  }
  return output;
}

// date-fns "dd"/"MM" require exactly two digits; "d"/"M" allow one or two. The lenient form is
// used for parsing on commit so single-digit entries still parse correctly.
export function toParseFormat(format: string): string {
  return format.replace(/dd/g, "d").replace(/MM/g, "M");
}

// Per-segment validation that allows partial digits while typing. Rejects letters and out-of-range
// numbers while still allowing intermediate edits like "0/12/2024" produced by deleting a digit.
export function isValidPartialDate(text: string, format: string): boolean {
  const separator = format.includes("/") ? "/" : "-";
  const segments = text.split(separator);
  const formatSegments = format.split(separator);
  if (segments.length > formatSegments.length) {
    return false;
  }
  for (let index = 0; index < segments.length; index++) {
    const segment = segments[index];
    const formatSegment = formatSegments[index];
    if (!/^\d*$/.test(segment)) {
      return false;
    }
    if (segment.length > formatSegment.length) {
      return false;
    }
    if (segment.length === 0) {
      continue;
    }
    const specification = dateFieldSpecifications[formatSegment[0]];
    if (!specification || !isPartialInRange(segment, specification)) {
      return false;
    }
  }
  return true;
}

// Two-digit year windowing: <30 → 2000-era, >=30 → 1900-era. Matches spreadsheet conventions.
export function expandShortYear(text: string, inputFormat: string): string {
  const separator = inputFormat.includes("/") ? "/" : "-";
  const textParts = text.split(separator);
  const formatParts = inputFormat.split(separator);
  if (textParts.length !== formatParts.length) {
    return text;
  }
  const adjustedParts = textParts.map((part, index) => {
    if (formatParts[index].toLowerCase() === "yyyy" && /^\d{1,2}$/.test(part)) {
      const yearNumber = Number(part);
      return String(yearNumber < 30 ? 2000 + yearNumber : 1900 + yearNumber);
    }
    return part;
  });
  return adjustedParts.join(separator);
}

export function parseIsoDate(iso: string | undefined): Date | undefined {
  if (!iso) {
    return undefined;
  }
  const date = new Date(`${iso}T00:00:00`);
  return Number.isNaN(date.getTime()) ? undefined : date;
}

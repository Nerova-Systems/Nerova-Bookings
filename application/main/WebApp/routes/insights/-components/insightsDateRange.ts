import { addDays, format, parseISO, startOfDay } from "date-fns";

// Shared closed-open [from, to) date-range helpers for the insights route. The page persists the
// selected range as `yyyy-MM-dd` strings in the URL search params; the API expects ISO datetimes.

export const DEFAULT_RANGE_DAYS = 30;

export function getDefaultRange(): { from: string; to: string } {
  // Backend treats the range as [From, To) so we add one full day to "today" to include today's bookings.
  const today = startOfDay(new Date());
  const to = addDays(today, 1);
  const from = addDays(today, -(DEFAULT_RANGE_DAYS - 1));
  return { from: format(from, "yyyy-MM-dd"), to: format(to, "yyyy-MM-dd") };
}

export function toRangeIso(from: string, to: string): { fromIso: string; toIso: string } {
  // The picker stores inclusive end dates; convert to the half-open ISO range the API expects.
  const fromDate = parseISO(from);
  const toDate = addDays(parseISO(to), 1);
  return { fromIso: fromDate.toISOString(), toIso: toDate.toISOString() };
}

export function pickerValue(from: string, to: string): { start: Date; end: Date } {
  return { start: parseISO(from), end: parseISO(to) };
}

import type { Schemas } from "@/shared/lib/api/client";

export type BookerState = "loading" | "selecting_date" | "selecting_time" | "booking";
export type PublicEventType = Schemas["PublicEventTypeResponse"];
export type PublicSlot = Schemas["PublicSlotResponse"];
export type AvailableSlot = PublicSlot & { startsAt: Date; label: string; value: string };

export function getAvailableSlots(
  slotsByDate: Record<string, PublicSlot[]>,
  selectedDate: Date | null
): AvailableSlot[] {
  if (!selectedDate) return [];
  return (slotsByDate[formatDateOnly(selectedDate)] ?? []).map((slot) => {
    const startsAt = new Date(slot.time);
    return { ...slot, startsAt, label: formatTime(startsAt), value: startsAt.toISOString() };
  });
}

export function getSlotRange(month: Date) {
  const start = new Date(Date.UTC(month.getFullYear(), month.getMonth(), 1, 0, 0, 0, 0));
  const end = new Date(Date.UTC(month.getFullYear(), month.getMonth() + 1, 1, 0, 0, 0, 0));
  return { start, end };
}

export function stringValue(value: unknown) {
  return typeof value === "string" && value.trim() ? value : undefined;
}

export function numberValue(value: unknown) {
  const number = typeof value === "string" ? Number(value) : undefined;
  return number && Number.isFinite(number) ? number : undefined;
}

export function parseDateOnly(value: string | undefined) {
  if (!value) return null;
  const date = new Date(`${value}T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function parseMonth(value: string | undefined) {
  if (!value) return null;
  const date = new Date(`${value}-01T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function parseSlotValue(value: string | null) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatDateOnly(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

export function formatMonth(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
}

export function formatLongDate(date: Date) {
  return new Intl.DateTimeFormat(undefined, { weekday: "long", month: "long", day: "numeric" }).format(date);
}

export function formatTime(date: Date) {
  return new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).format(date);
}

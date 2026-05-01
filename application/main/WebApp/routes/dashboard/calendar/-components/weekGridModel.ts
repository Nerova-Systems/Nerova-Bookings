import type { Appointment, AvailabilityRule, BusinessClosure, CalendarBlock } from "@/shared/lib/appointmentsApi";

import { formatDayNumber, formatWeekday } from "@/shared/lib/dateFormatting";

type EventType = "confirmed" | "pending" | "sync" | "blocked";

interface PositionedItem {
  label: string;
  type: EventType;
  topPct: number;
  heightPct: number;
  zIndex: number;
}

interface AvailabilityBand {
  startTime: string;
  endTime: string;
  topPct: number;
  heightPct: number;
}

interface DayColumn {
  dateKey: string;
  day: string;
  dayNumber: string;
  isToday: boolean;
  bands: AvailabilityBand[];
  closure?: BusinessClosure;
  events: PositionedItem[];
}

export function buildDays(
  appointments: Appointment[],
  blocks: CalendarBlock[],
  availabilityRules: AvailabilityRule[],
  closures: BusinessClosure[],
  weekStart: Date,
  range: { startHour: number; totalMinutes: number }
): DayColumn[] {
  const todayKey = dateKey(new Date());

  return Array.from({ length: 7 }, (_, index) => {
    const date = new Date(weekStart);
    date.setDate(weekStart.getDate() + index);
    const key = dateKey(date);
    const dayName = fullDayName(date);
    const closure = closures.find((item) => item.startDate <= key && item.endDate >= key);
    const bands = availabilityRules
      .filter((rule) => rule.dayOfWeek === dayName)
      .map((rule) => toBand(rule, range.startHour, range.totalMinutes));
    const events = [
      ...appointments
        .filter((appointment) => dateKey(new Date(appointment.startAt)) === key)
        .map((appointment) => toAppointmentItem(appointment, range.startHour, range.totalMinutes)),
      ...blocks
        .filter((block) => dateKey(new Date(block.startAt)) === key)
        .map((block) => toBlockItem(block, range.startHour, range.totalMinutes))
    ].sort((a, b) => a.topPct - b.topPct || a.zIndex - b.zIndex);

    return {
      dateKey: key,
      day: formatWeekday(date),
      dayNumber: formatDayNumber(date),
      isToday: key === todayKey,
      bands,
      closure,
      events
    };
  });
}

export function buildHourRange(
  appointments: Appointment[],
  blocks: CalendarBlock[],
  availabilityRules: AvailabilityRule[],
  weekStart: Date
) {
  const weekEnd = new Date(weekStart);
  weekEnd.setDate(weekStart.getDate() + 7);
  const starts = [7 * 60];
  const ends = [18 * 60];

  for (const rule of availabilityRules) {
    starts.push(parseTime(rule.startTime));
    ends.push(parseTime(rule.endTime));
  }

  for (const item of [...appointments, ...blocks]) {
    const start = new Date(item.startAt);
    const end = new Date(item.endAt);
    if (start >= weekStart && start < weekEnd) {
      starts.push(start.getHours() * 60 + start.getMinutes());
      ends.push(end.getHours() * 60 + end.getMinutes());
    }
  }

  const startHour = Math.max(0, Math.floor(Math.min(...starts) / 60));
  const endHour = Math.min(24, Math.ceil(Math.max(...ends) / 60));
  return { startHour, endHour, totalMinutes: (endHour - startHour) * 60 };
}

export function eventClasses(type: EventType): string {
  if (type === "confirmed") return "border-emerald-400/35 bg-emerald-400/15 text-emerald-50";
  if (type === "pending") return "border-amber-400/35 bg-amber-400/15 text-amber-50";
  if (type === "sync") return "border-sky-300/25 bg-sky-300/10 text-sky-50";
  return "border-white/20 bg-white/10 text-[#e9e9e9]";
}

export function hourGrid(startHour: number, endHour: number) {
  const hourCount = endHour - startHour;
  return `repeating-linear-gradient(to bottom, transparent 0, transparent calc(100% / ${hourCount} - 1px), rgba(255,255,255,0.09) calc(100% / ${hourCount} - 1px), rgba(255,255,255,0.09) calc(100% / ${hourCount}))`;
}

export function minutesFromStart(date: Date, startHour: number) {
  return (date.getHours() - startHour) * 60 + date.getMinutes();
}

export function formatHour(hour: number) {
  const normalized = hour % 24;
  if (normalized === 0) return "12:00am";
  if (normalized < 12) return `${normalized}:00am`;
  if (normalized === 12) return "12:00pm";
  return `${normalized - 12}:00pm`;
}

export function gmtLabel(timeZone: string) {
  return timeZone === "Africa/Johannesburg" ? "GMT +2" : timeZone;
}

function toBand(rule: AvailabilityRule, startHour: number, totalMinutes: number): AvailabilityBand {
  const start = parseTime(rule.startTime) - startHour * 60;
  const end = parseTime(rule.endTime) - startHour * 60;
  return {
    startTime: rule.startTime,
    endTime: rule.endTime,
    topPct: clampPct((start / totalMinutes) * 100),
    heightPct: Math.max(1, ((end - start) / totalMinutes) * 100)
  };
}

function toAppointmentItem(appointment: Appointment, startHour: number, totalMinutes: number): PositionedItem {
  const start = new Date(appointment.startAt);
  const end = new Date(appointment.endAt);
  const type = appointment.status === "confirmed" ? "confirmed" : "pending";
  return {
    label: `${appointment.time} ${appointment.name} - ${appointment.statusLabel} - v${appointment.serviceVersionNumber}`,
    type,
    ...position(start, end, startHour, totalMinutes),
    zIndex: 10
  };
}

function toBlockItem(block: CalendarBlock, startHour: number, totalMinutes: number): PositionedItem {
  const start = new Date(block.startAt);
  const end = new Date(block.endAt);
  return {
    label: block.type === "manual" ? `Blocked - ${block.title}` : `${block.title} - Sync`,
    type: block.type === "manual" ? "blocked" : "sync",
    ...position(start, end, startHour, totalMinutes),
    zIndex: 9
  };
}

function position(start: Date, end: Date, startHour: number, totalMinutes: number) {
  const startMinutes = minutesFromStart(start, startHour);
  const duration = Math.max(30, (end.getTime() - start.getTime()) / 60000);
  return {
    topPct: clampPct((startMinutes / totalMinutes) * 100),
    heightPct: Math.max(3.5, (duration / totalMinutes) * 100)
  };
}

function parseTime(value: string) {
  const [hour, minute] = value.split(":").map(Number);
  return hour * 60 + minute;
}

function dateKey(date: Date) {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function fullDayName(date: Date) {
  return ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"][date.getDay()];
}

function clampPct(value: number) {
  return Math.max(0, Math.min(100, value));
}

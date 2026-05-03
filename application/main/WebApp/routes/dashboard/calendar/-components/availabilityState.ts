import type { AvailabilityRule } from "@/shared/lib/appointmentsApi";

export interface WindowState {
  startTime: string;
  endTime: string;
}

export interface DayState {
  dayOfWeek: string;
  enabled: boolean;
  windows: WindowState[];
}

export const DAYS = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

export function buildInitialDays(rules: AvailabilityRule[]): DayState[] {
  return DAYS.map((dayOfWeek) => {
    const windows = rules
      .filter((rule) => rule.dayOfWeek === dayOfWeek)
      .map((rule) => ({ startTime: rule.startTime, endTime: rule.endTime }));
    return {
      dayOfWeek,
      enabled: windows.length > 0,
      windows: windows.length > 0 ? windows : [{ startTime: "09:00", endTime: "17:00" }]
    };
  });
}

export function updateDay(
  days: DayState[],
  setDays: (days: DayState[]) => void,
  dayIndex: number,
  patch: Partial<DayState>
) {
  setDays(days.map((day, index) => (index === dayIndex ? { ...day, ...patch } : day)));
}

export function updateWindow(
  days: DayState[],
  setDays: (days: DayState[]) => void,
  dayIndex: number,
  windowIndex: number,
  patch: Partial<WindowState>
) {
  const day = days[dayIndex];
  updateDay(days, setDays, dayIndex, {
    windows: day.windows.map((window, index) => (index === windowIndex ? { ...window, ...patch } : window))
  });
}

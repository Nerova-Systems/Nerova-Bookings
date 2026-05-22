import { create } from "zustand";
import { persist } from "zustand/middleware";

/**
 * Zustand store for user time display preferences.
 * Ported from cal.com `packages/store/timePreferences.ts` (cf2a55c).
 *
 * Persisted to localStorage under the key `nerova:time-preferences`.
 *
 * Usage:
 *   const { timeFormat, timezone, setTimeFormat, setTimezone } = useTimePreferences();
 */

/** 12-hour or 24-hour clock display. */
export type TimeFormat = 12 | 24;

interface TimePreferencesState {
  /** Whether to use 12h or 24h time format. `null` = follow browser/locale default. */
  timeFormat: TimeFormat | null;
  /** IANA timezone string. `null` = follow browser default. */
  timezone: string | null;
  /** Derived: 24h if set, else null for locale-based formatting. */
  use24hTime: boolean | null;

  setTimeFormat: (format: TimeFormat | null) => void;
  setTimezone: (tz: string | null) => void;
}

export const useTimePreferences = create<TimePreferencesState>()(
  persist(
    (set, get) => ({
      timeFormat: null,
      timezone: null,
      get use24hTime() {
        const { timeFormat } = get();
        if (timeFormat === null) return null;
        return timeFormat === 24;
      },

      setTimeFormat: (format) => set({ timeFormat: format }),
      setTimezone: (tz) => set({ timezone: tz })
    }),
    {
      name: "nerova:time-preferences",
      partialize: (state) => ({
        timeFormat: state.timeFormat,
        timezone: state.timezone
      })
    }
  )
);

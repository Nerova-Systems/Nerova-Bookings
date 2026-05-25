/**
 * Cross-app reader for the current user's preferences (TimeFormat, WeekStart, Language, TimeZone).
 *
 * Lives in shared-webapp so both `account/WebApp` (where the settings UI ships) and `main/WebApp`
 * (where bookings/calendar/availability surfaces consume the values) can read the same cached
 * source-of-truth without round-tripping through per-app generated OpenAPI clients.
 *
 * Backend contract: `GET /api/account/users/me/preferences` returns `UserPreferencesResponse`
 * (always populated — defaults to 24-hour / Monday / en-US / UTC for users with no row yet).
 * `PATCH` accepts the same shape with all fields nullable for partial updates.
 */
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { enhancedFetch } from "../http/httpClient";

export const TimeFormat = {
  TwelveHour: "TwelveHour",
  TwentyFourHour: "TwentyFourHour"
} as const;

export type TimeFormatValue = (typeof TimeFormat)[keyof typeof TimeFormat];

/** Matches .NET `DayOfWeek` enum values returned by the API. */
export type DayOfWeekValue = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export interface UserPreferences {
  timeFormat: TimeFormatValue;
  weekStart: DayOfWeekValue;
  language: string;
  timeZone: string;
}

export interface UpdateUserPreferencesPayload {
  timeFormat?: TimeFormatValue | null;
  weekStart?: DayOfWeekValue | null;
  language?: string | null;
  timeZone?: string | null;
}

const PREFERENCES_ENDPOINT = "/api/account/users/me/preferences";

/** Shared cache key — keep aligned with the typed openapi-react-query key so PATCH invalidation hits both. */
export const userPreferencesQueryKey = ["get", PREFERENCES_ENDPOINT] as const;

export const defaultUserPreferences: UserPreferences = {
  timeFormat: TimeFormat.TwentyFourHour,
  weekStart: "Monday",
  language: "en-US",
  timeZone: "UTC"
};

async function fetchUserPreferences(): Promise<UserPreferences> {
  const response = await enhancedFetch(PREFERENCES_ENDPOINT, { method: "GET" });
  return (await response.json()) as UserPreferences;
}

async function patchUserPreferences(payload: UpdateUserPreferencesPayload): Promise<UserPreferences> {
  const response = await enhancedFetch(PREFERENCES_ENDPOINT, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  return (await response.json()) as UserPreferences;
}

/**
 * Returns the current user's preferences, falling back to defaults until the request settles.
 * Safe to call from any component in any of the federated webapps.
 */
export function useUserPreferences(): UserPreferences {
  const { data } = useQuery({
    queryKey: userPreferencesQueryKey,
    queryFn: fetchUserPreferences,
    // Preferences change infrequently — once a session is usually enough.
    staleTime: 5 * 60 * 1000,
    gcTime: 30 * 60 * 1000
  });
  return data ?? defaultUserPreferences;
}

/**
 * Returns the raw query result so callers can render skeletons / surface fetch errors.
 * Most call sites should prefer {@link useUserPreferences}.
 */
export function useUserPreferencesQuery() {
  return useQuery({
    queryKey: userPreferencesQueryKey,
    queryFn: fetchUserPreferences,
    staleTime: 5 * 60 * 1000,
    gcTime: 30 * 60 * 1000
  });
}

export function useUpdateUserPreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: patchUserPreferences,
    onSuccess: (updated) => {
      queryClient.setQueryData(userPreferencesQueryKey, updated);
      // Also bust the `/users/me` cache: the response embeds `preferences` and consumers may
      // read it from there. Match whichever key shape the per-app openapi-react-query client uses.
      queryClient.invalidateQueries({ queryKey: ["get", "/api/account/users/me"] });
    }
  });
}

/**
 * Builds `Intl.DateTimeFormatOptions` defaults aligned with the user's preferences.
 * Pass to {@link Intl.DateTimeFormat} alongside any per-call overrides.
 */
export function preferencesToTimeFormatOptions(
  preferences: UserPreferences
): Pick<Intl.DateTimeFormatOptions, "hour12"> {
  return { hour12: preferences.timeFormat === TimeFormat.TwelveHour };
}

const dayOfWeekIndex: Record<DayOfWeekValue, number> = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6
};

/** Numeric (0=Sun..6=Sat) representation of the user's preferred first day of week. */
export function getWeekStartIndex(preferences: UserPreferences): number {
  return dayOfWeekIndex[preferences.weekStart];
}

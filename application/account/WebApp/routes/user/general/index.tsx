import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import localeMap from "@repo/infrastructure/translations/i18n.config.json";
import {
  defaultUserPreferences,
  type DayOfWeekValue,
  TimeFormat,
  type TimeFormatValue,
  type UpdateUserPreferencesPayload,
  type UserPreferences,
  useUpdateUserPreferences,
  useUserPreferencesQuery
} from "@repo/infrastructure/userPreferences/UserPreferencesContext";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { SegmentedControl, SegmentedControlItem, SegmentedControlList } from "@repo/ui/components/SegmentedControl";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { TimeZonePicker } from "@repo/ui/components/TimeZonePicker";
import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

export const Route = createFileRoute("/user/general/")({
  staticData: { trackingTitle: "General preferences" },
  component: GeneralPreferencesPage
});

const SUPPORTED_LANGUAGES = Object.keys(localeMap) as readonly string[];

const DAYS_OF_WEEK: readonly { value: DayOfWeekValue; label: string }[] = [
  { value: "Sunday", label: t`Sunday` },
  { value: "Monday", label: t`Monday` },
  { value: "Tuesday", label: t`Tuesday` },
  { value: "Wednesday", label: t`Wednesday` },
  { value: "Thursday", label: t`Thursday` },
  { value: "Friday", label: t`Friday` },
  { value: "Saturday", label: t`Saturday` }
];

const IANA_TIMEZONE_PATTERN = /^[A-Za-z]+(?:[/_+-][A-Za-z0-9]+)*$/;

function isValidIanaTimeZone(value: string): boolean {
  if (!value || !IANA_TIMEZONE_PATTERN.test(value)) return false;
  try {
    // Browsers throw RangeError for unknown IANA IDs. Server validates definitively.
    Intl.DateTimeFormat(undefined, { timeZone: value });
    return true;
  } catch {
    return false;
  }
}

/**
 * Returns the subset of `next` that differs from `current`, so PATCH only carries actual edits.
 */
function diffPreferences(current: UserPreferences, next: UserPreferences): UpdateUserPreferencesPayload {
  const changes: UpdateUserPreferencesPayload = {};
  if (current.timeFormat !== next.timeFormat) changes.timeFormat = next.timeFormat;
  if (current.weekStart !== next.weekStart) changes.weekStart = next.weekStart;
  if (current.language !== next.language) changes.language = next.language;
  if (current.timeZone !== next.timeZone) changes.timeZone = next.timeZone;
  return changes;
}

function GeneralPreferencesPage() {
  const { data: serverPreferences, isLoading } = useUserPreferencesQuery();
  const updateMutation = useUpdateUserPreferences();

  // Local draft state — initialised from server, edited freely until Save.
  const baseline = serverPreferences ?? defaultUserPreferences;
  const [draft, setDraft] = useState<UserPreferences>(baseline);

  // Sync the draft when the server data first arrives (or after a successful save refreshes it).
  // Intentionally keyed on the four primitive fields so this doesn't fight in-progress edits when
  // the underlying object identity changes for an unrelated reason (e.g., refetch on focus).
  useEffect(() => {
    if (!serverPreferences) return;
    setDraft(serverPreferences);
  }, [serverPreferences]);

  const changes = useMemo(() => diffPreferences(baseline, draft), [baseline, draft]);
  const hasChanges = Object.keys(changes).length > 0;
  const timeZoneInvalid = draft.timeZone.length > 0 && !isValidIanaTimeZone(draft.timeZone);

  const handleSave = () => {
    if (!hasChanges || timeZoneInvalid) return;
    updateMutation.mutate(changes, {
      onSuccess: () => {
        toast.success(t`Preferences saved`);
      },
      onError: () => {
        toast.error(t`Could not save preferences. Please try again.`);
      }
    });
  };

  return (
    <AppLayout
      variant="center"
      maxWidth="48rem"
      balanceWidth="16rem"
      title={t`General`}
      subtitle={t`Manage how dates, times, and language are presented across the app.`}
    >
      <div className="flex flex-col gap-8 pt-8">
        <section className="flex flex-col gap-2">
          <h3 className="font-medium">
            <Trans>Language</Trans>
          </h3>
          <p className="text-sm text-muted-foreground">
            <Trans>The language used throughout the application.</Trans>
          </p>
          <Select
            value={draft.language}
            onValueChange={(value) => value && setDraft((d) => ({ ...d, language: value }))}
          >
            <SelectTrigger className="w-full sm:w-80">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {SUPPORTED_LANGUAGES.map((code) => (
                <SelectItem key={code} value={code}>
                  {(localeMap as Record<string, { label: string }>)[code]?.label ?? code}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </section>

        <section className="flex flex-col gap-2">
          <h3 className="font-medium">
            <Trans>Time format</Trans>
          </h3>
          <p className="text-sm text-muted-foreground">
            <Trans>Choose between a 12-hour or 24-hour clock display.</Trans>
          </p>
          <SegmentedControl
            value={draft.timeFormat}
            onValueChange={(value) =>
              setDraft((d) => ({ ...d, timeFormat: (value as TimeFormatValue) ?? d.timeFormat }))
            }
          >
            <SegmentedControlList>
              <SegmentedControlItem value={TimeFormat.TwelveHour}>
                <Trans>12 hour</Trans>
              </SegmentedControlItem>
              <SegmentedControlItem value={TimeFormat.TwentyFourHour}>
                <Trans>24 hour</Trans>
              </SegmentedControlItem>
            </SegmentedControlList>
          </SegmentedControl>
        </section>

        <section className="flex flex-col gap-2">
          <h3 className="font-medium">
            <Trans>Start of week</Trans>
          </h3>
          <p className="text-sm text-muted-foreground">
            <Trans>The first day shown in calendar views and weekly schedules.</Trans>
          </p>
          <Select
            value={draft.weekStart}
            onValueChange={(value) => value && setDraft((d) => ({ ...d, weekStart: value as DayOfWeekValue }))}
          >
            <SelectTrigger className="w-full sm:w-80">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {DAYS_OF_WEEK.map((day) => (
                <SelectItem key={day.value} value={day.value}>
                  {day.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </section>

        <section className="flex flex-col gap-2">
          <h3 className="font-medium">
            <Trans>Time zone</Trans>
          </h3>
          <p className="text-sm text-muted-foreground">
            <Trans>Used to display booking times in your local time.</Trans>
          </p>
          <div className="w-full sm:w-80">
            <TimeZonePicker
              value={draft.timeZone}
              onValueChange={(value) => setDraft((d) => ({ ...d, timeZone: value ?? d.timeZone }))}
              errorMessage={timeZoneInvalid ? t`Enter a valid IANA time zone (e.g. Europe/Copenhagen).` : undefined}
            />
          </div>
        </section>

        <div className="flex justify-end">
          <Button
            type="button"
            onClick={handleSave}
            disabled={isLoading || !hasChanges || timeZoneInvalid}
            isPending={updateMutation.isPending}
          >
            {updateMutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save changes</Trans>}
          </Button>
        </div>
      </div>
    </AppLayout>
  );
}

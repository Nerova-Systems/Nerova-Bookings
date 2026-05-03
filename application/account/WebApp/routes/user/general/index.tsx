import type { ReactNode } from "react";

/* eslint-disable max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { ToggleGroup, ToggleGroupItem } from "@repo/ui/components/ToggleGroup";
import { createFileRoute } from "@tanstack/react-router";
import { CalendarClockIcon, MoonIcon, SunIcon } from "lucide-react";
import { toast } from "sonner";

import { ThemeMode, locales, usePreferences } from "../preferences/-components/usePreferences";

export const Route = createFileRoute("/user/general/")({
  staticData: { trackingTitle: "General" },
  component: GeneralPage
});

function GeneralPage() {
  const {
    theme,
    currentLocale,
    currentZoomLevel,
    zoomLevelOptions,
    handleLocaleChange,
    handleZoomLevelChange,
    handleThemeChange,
    getSystemThemeIcon
  } = usePreferences();

  const showShellToast = () => toast.info(t`This setting will be connected in the next account settings pass.`);

  return (
    <AppLayout
      variant="center"
      maxWidth="64rem"
      balanceWidth="16rem"
      title={t`General`}
      subtitle={t`Manage settings for your language, timezone, and interface.`}
    >
      <div className="flex flex-col gap-4 pt-6">
        <section className="rounded-xl border border-border bg-card">
          <div className="grid gap-6 p-5 md:grid-cols-2">
            <div className="grid gap-2">
              <span id="general-language-label" className="text-sm font-semibold">
                <Trans>Language</Trans>
              </span>
              <select
                id="general-language"
                aria-labelledby="general-language-label"
                value={currentLocale}
                onChange={(event) => handleLocaleChange(event.target.value as (typeof locales)[number]["id"])}
                className="h-10 rounded-md border border-input bg-background px-3 text-sm outline-ring focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                {locales.map((locale) => (
                  <option key={locale.id} value={locale.id}>
                    {locale.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="grid gap-2">
              <span id="general-timezone-label" className="text-sm font-semibold">
                <Trans>Timezone</Trans>
              </span>
              <select
                id="general-timezone"
                aria-labelledby="general-timezone-label"
                value="Africa/Johannesburg"
                onChange={showShellToast}
                className="h-10 rounded-md border border-input bg-background px-3 text-sm outline-ring focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                <option value="Africa/Johannesburg">Africa/Johannesburg</option>
              </select>
            </div>

            <div className="grid gap-2">
              <span id="general-time-format-label" className="text-sm font-semibold">
                <Trans>Time format</Trans>
              </span>
              <select
                id="general-time-format"
                aria-labelledby="general-time-format-label"
                value="12-hour"
                onChange={showShellToast}
                className="h-10 rounded-md border border-input bg-background px-3 text-sm outline-ring focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                <option value="12-hour">12-hour</option>
                <option value="24-hour">24-hour</option>
              </select>
            </div>

            <div className="grid gap-2">
              <span id="general-start-of-week-label" className="text-sm font-semibold">
                <Trans>Start of week</Trans>
              </span>
              <select
                id="general-start-of-week"
                aria-labelledby="general-start-of-week-label"
                value="Sunday"
                onChange={showShellToast}
                className="h-10 rounded-md border border-input bg-background px-3 text-sm outline-ring focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                <option value="Sunday">Sunday</option>
                <option value="Monday">Monday</option>
              </select>
            </div>
          </div>
          <div className="flex justify-end border-t border-border bg-muted/30 px-5 py-3">
            <button
              type="button"
              onClick={() => toast.success(t`General settings saved.`)}
              className="h-9 rounded-md bg-primary px-4 text-sm font-semibold text-primary-foreground"
            >
              <Trans>Update</Trans>
            </button>
          </div>
        </section>

        <PreferenceSection title={t`Theme`} description={t`Choose how the application looks to you on this device.`}>
          <ToggleGroup
            variant="outline"
            size="lg"
            className="flex w-full"
            value={[theme ?? ThemeMode.System]}
            onValueChange={(values) => {
              if (values.length > 0) handleThemeChange(values[0]);
            }}
          >
            <ToggleGroupItem className="flex-1" value={ThemeMode.System}>
              {getSystemThemeIcon()}
              <Trans>System</Trans>
            </ToggleGroupItem>
            <ToggleGroupItem className="flex-1" value={ThemeMode.Light}>
              <SunIcon className="size-5" />
              <Trans>Light</Trans>
            </ToggleGroupItem>
            <ToggleGroupItem className="flex-1" value={ThemeMode.Dark}>
              <MoonIcon className="size-5" />
              <Trans>Dark</Trans>
            </ToggleGroupItem>
          </ToggleGroup>
        </PreferenceSection>

        <PreferenceSection
          title={t`Zoom`}
          description={t`Adjust the interface size on this device to your preference.`}
        >
          <ToggleGroup
            variant="outline"
            size="lg"
            className="flex w-full"
            value={[currentZoomLevel]}
            onValueChange={(values) => {
              if (values.length > 0) handleZoomLevelChange(values[0]);
            }}
          >
            {zoomLevelOptions.map((zoom) => (
              <ToggleGroupItem key={zoom.value} className="flex-1" value={zoom.value}>
                <span style={{ fontSize: zoom.fontSize, lineHeight: 1 }}>Aa</span>
                {zoom.label}
              </ToggleGroupItem>
            ))}
          </ToggleGroup>
        </PreferenceSection>

        <PreferenceSwitch
          title={t`Dynamic group links`}
          description={t`Allow attendees to book through dynamic group bookings`}
        />
        <PreferenceSwitch
          title={t`Allow search engine indexing`}
          description={t`Allow search engines to access your public content`}
        />
        <PreferenceSwitch title={t`Monthly digest email`} description={t`Monthly digest email for teams`} />
        <PreferenceSwitch
          title={t`Prevent impersonation on bookings`}
          description={t`When enabled, anyone trying to book events using your email address must verify they own it.`}
        />
      </div>
    </AppLayout>
  );
}

function PreferenceSection({
  title,
  description,
  children
}: {
  title: string;
  description: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-xl border border-border bg-card p-5">
      <h3 className="text-lg font-semibold">{title}</h3>
      <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      <div className="mt-4">{children}</div>
    </section>
  );
}

function PreferenceSwitch({ title, description }: { title: string; description: string }) {
  return (
    <button
      type="button"
      onClick={() => toast.info(t`This preference will be connected in the next account settings pass.`)}
      className="flex min-h-16 items-center gap-4 rounded-xl border border-border bg-card px-5 py-4 text-left transition-colors hover:bg-muted/40"
    >
      <CalendarClockIcon className="size-5 text-muted-foreground" />
      <span className="min-w-0 flex-1">
        <span className="block font-semibold">{title}</span>
        <span className="block text-sm text-muted-foreground">{description}</span>
      </span>
      <span className="relative h-5 w-9 rounded-full bg-muted">
        <span className="absolute top-0.5 right-0.5 size-4 rounded-full bg-background shadow-sm" />
      </span>
    </button>
  );
}

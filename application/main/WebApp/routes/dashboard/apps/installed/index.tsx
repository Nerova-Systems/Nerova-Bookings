import { t } from "@lingui/core/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, Link } from "@tanstack/react-router";
import {
  BarChart3Icon,
  BotIcon,
  CalendarDaysIcon,
  CreditCardIcon,
  Grid3X3Icon,
  MailIcon,
  PlusIcon,
  UsersIcon,
  VideoIcon
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import { useAppointmentShell } from "@/shared/lib/appointmentsApi";

import { AppLogo } from "../-components/AppLogo";
import { INSTALLED_CATEGORIES } from "../-components/appCategories";
import { APP_CATALOG, type AppCategory } from "../-components/appCatalog";

export const Route = createFileRoute("/dashboard/apps/installed/")({
  staticData: { trackingTitle: "Installed apps" },
  component: InstalledAppsPage
});

const CATEGORY_ICONS = {
  Analytics: BarChart3Icon,
  "AI & Automation": BotIcon,
  Calendar: CalendarDaysIcon,
  Conferencing: VideoIcon,
  CRM: UsersIcon,
  Messaging: MailIcon,
  Payment: CreditCardIcon,
  Other: Grid3X3Icon
};

function InstalledAppsPage() {
  const [category, setCategory] = useState<AppCategory>("Calendar");
  const shellQuery = useAppointmentShell();
  const googleCalendar = APP_CATALOG.find((app) => app.slug === "google-calendar") ?? APP_CATALOG[0];
  const calendarIntegration = useMemo(
    () => shellQuery.data?.integrations.find((integration) => integration.provider === "Google" && integration.capability === "Calendar"),
    [shellQuery.data?.integrations]
  );

  useEffect(() => {
    document.title = t`Installed apps | Nerova`;
  }, []);

  return (
    <main className="flex min-h-0 flex-1 flex-col overflow-y-auto bg-[#0f0f0f] px-8 py-8 text-white">
      <header>
        <h1 className="font-display text-3xl font-semibold">Installed apps</h1>
        <p className="mt-1 text-lg text-white/75">Manage your installed apps or change settings</p>
      </header>

      <div className="mt-12 grid min-h-0 grid-cols-[17rem_1fr] gap-8">
        <aside className="space-y-1">
          {INSTALLED_CATEGORIES.map((item) => {
            const Icon = CATEGORY_ICONS[item];
            return (
              <button
                key={item}
                type="button"
                onClick={() => setCategory(item)}
                className={`flex h-12 w-full items-center gap-3 rounded-xl px-4 text-left text-lg font-semibold transition-colors ${
                  category === item ? "bg-white/[0.08] text-white" : "text-white/85 hover:bg-white/5"
                }`}
              >
                <Icon className="size-5" />
                <span>{item}</span>
              </button>
            );
          })}
        </aside>

        <section className="min-w-0">
          {category === "Calendar" ? (
            <CalendarInstalledSettings app={googleCalendar} status={calendarIntegration?.status ?? "Demo"} />
          ) : (
            <EmptyInstalledCategory category={category} />
          )}
        </section>
      </div>
    </main>
  );
}

function CalendarInstalledSettings({ app, status }: { app: typeof APP_CATALOG[number]; status: string }) {
  return (
    <div>
      <div className="mb-9 flex flex-wrap items-start gap-4">
        <div>
          <h2 className="font-display text-3xl font-semibold">Calendars</h2>
          <p className="mt-1 text-lg text-white/50">Configure how your event types interact with your calendars</p>
        </div>
        <Button type="button" variant="outline" className="ml-auto border-white/15 bg-transparent text-white hover:bg-white/[0.08]">
          <PlusIcon className="size-4" />
          Add calendar
        </Button>
      </div>

      <section className="overflow-hidden rounded-3xl border border-white/10 bg-[#202020]">
        <div className="px-9 py-7">
          <h3 className="text-xl font-semibold">Add to calendar</h3>
          <p className="mt-1 text-lg text-white/45">Select where to add events when you're booked.</p>
        </div>
        <div className="grid gap-9 rounded-t-3xl border-t border-white/10 bg-[#191919] px-9 py-8 md:grid-cols-2">
          <SettingSelect
            label="Add events to"
            value="colinswart0@gmail.com (Google - colinswart...)"
            help="You can override this on a per-event basis in the advanced settings in each event type."
          />
          <SettingSelect
            label="Default reminder"
            value="Use default reminders"
            help="Set the default reminder time for events added to your Google Calendar."
          />
        </div>
      </section>

      <section className="mt-6 overflow-hidden rounded-3xl border border-white/10 bg-[#202020]">
        <div className="flex items-start gap-4 px-9 py-7">
          <div>
            <h3 className="text-xl font-semibold">Check for conflicts</h3>
            <p className="mt-1 text-lg text-white/45">Select which calendars you want to check for conflicts to prevent double bookings.</p>
          </div>
          <Button type="button" variant="outline" className="ml-auto border-white/15 bg-transparent text-white hover:bg-white/[0.08]">
            <PlusIcon className="size-4" />
            Add
          </Button>
        </div>
        <Link
          to="/dashboard/apps/$appSlug"
          params={{ appSlug: app.slug }}
          className="flex items-center gap-5 border-t border-white/10 bg-[#191919] px-9 py-6 transition-colors hover:bg-[#202020]"
        >
          <AppLogo app={app} size="sm" />
          <div className="min-w-0">
            <div className="text-xl font-semibold">{app.name}</div>
            <div className="truncate text-lg text-white/45">colinswart0@gmail.com</div>
          </div>
          <span className="ml-auto rounded-full border border-white/10 px-3 py-1 text-xs text-white/55">{status}</span>
        </Link>
      </section>
    </div>
  );
}

function SettingSelect({ label, value, help }: { label: string; value: string; help: string }) {
  return (
    <div>
      <label className="text-lg font-semibold">{label}</label>
      <select
        disabled
        value={value}
        className="mt-3 h-12 w-full rounded-xl border border-white/10 bg-[#202020] px-4 text-base font-semibold text-white/80 outline-none disabled:opacity-80"
      >
        <option>{value}</option>
      </select>
      <p className="mt-3 text-base leading-snug text-white/40">{help}</p>
    </div>
  );
}

function EmptyInstalledCategory({ category }: { category: AppCategory }) {
  return (
    <div className="rounded-3xl border border-white/10 bg-[#202020] px-9 py-8">
      <h2 className="font-display text-3xl font-semibold">{category}</h2>
      <p className="mt-2 text-lg text-white/50">Installed app settings for this category will appear here.</p>
    </div>
  );
}

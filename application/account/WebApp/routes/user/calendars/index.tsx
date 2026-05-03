import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { Loader2Icon, PlusIcon, RefreshCwIcon } from "lucide-react";
import { toast } from "sonner";

import {
  useCreateIntegrationConnectSession,
  useMainAppointmentShell,
  useSyncIntegrationConnections
} from "@/shared/lib/mainAppSettingsApi";

export const Route = createFileRoute("/user/calendars/")({
  staticData: { trackingTitle: "Calendars" },
  component: CalendarsPage
});

function CalendarsPage() {
  const shellQuery = useMainAppointmentShell();
  const connectSessionMutation = useCreateIntegrationConnectSession();
  const syncConnectionsMutation = useSyncIntegrationConnections();
  const calendarIntegration = shellQuery.data?.integrations.find(
    (integration) => integration.provider === "Google" && integration.capability === "Calendar"
  );
  const status = calendarIntegration?.status ?? "Not connected";
  const actionPending = connectSessionMutation.isPending || syncConnectionsMutation.isPending;

  async function handleAddCalendar() {
    try {
      const session = await connectSessionMutation.mutateAsync({ appSlug: "google-calendar" });
      window.open(session.connectLink, "_blank", "noopener,noreferrer");
      toast.success(t`Finish Google Calendar authorization in the new tab, then refresh status here.`);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t`Could not start Google Calendar connection.`);
    }
  }

  async function handleRefreshStatus() {
    try {
      await syncConnectionsMutation.mutateAsync({ appSlug: "google-calendar" });
      toast.success(t`Google Calendar connection status refreshed.`);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t`Could not refresh Google Calendar status.`);
    }
  }

  return (
    <AppLayout
      variant="center"
      maxWidth="70rem"
      balanceWidth="16rem"
      title={t`Calendars`}
      subtitle={t`Configure how your event types interact with your calendars`}
    >
      <div className="flex flex-col gap-4 pt-6">
        <div className="flex justify-end">
          <Button type="button" variant="outline" disabled={actionPending} onClick={handleAddCalendar}>
            {connectSessionMutation.isPending ? (
              <Loader2Icon className="size-4 animate-spin" />
            ) : (
              <PlusIcon className="size-4" />
            )}
            <Trans>Add calendar</Trans>
          </Button>
          <Button
            type="button"
            variant="outline"
            className="ml-2"
            disabled={actionPending}
            onClick={handleRefreshStatus}
          >
            {syncConnectionsMutation.isPending ? (
              <Loader2Icon className="size-4 animate-spin" />
            ) : (
              <RefreshCwIcon className="size-4" />
            )}
            <Trans>Refresh status</Trans>
          </Button>
        </div>

        <section className="overflow-hidden rounded-xl border border-border bg-card">
          <div className="px-5 py-4">
            <h2 className="text-lg font-semibold">
              <Trans>Add to calendar</Trans>
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              <Trans>Select where to add events when you're booked.</Trans>
            </p>
          </div>
          <div className="grid gap-5 border-t border-border bg-muted/20 px-5 py-5 md:grid-cols-2">
            <SettingSelect
              label={t`Add events to`}
              value="colinswart0@gmail.com (Google - colinswart...)"
              help={t`You can override this on a per-event basis in the advanced settings in each event type.`}
            />
            <SettingSelect
              label={t`Default reminder`}
              value="Use default reminders"
              help={t`Set the default reminder time for events added to your Google Calendar.`}
            />
          </div>
        </section>

        <section className="overflow-hidden rounded-xl border border-border bg-card">
          <div className="flex items-start gap-4 px-5 py-4">
            <div>
              <h2 className="text-lg font-semibold">
                <Trans>Check for conflicts</Trans>
              </h2>
              <p className="mt-1 text-sm text-muted-foreground">
                <Trans>Select which calendars you want to check for conflicts to prevent double bookings.</Trans>
              </p>
            </div>
          </div>
          <div className="border-t border-border bg-muted/20 px-5 py-4">
            <div className="flex items-center gap-4">
              <GoogleCalendarLogo />
              <div className="min-w-0">
                <div className="font-semibold">Google Calendar</div>
                <div className="truncate text-sm text-muted-foreground">colinswart0@gmail.com</div>
              </div>
              <span className="ml-auto rounded-full border border-border px-2 py-1 text-xs text-muted-foreground">
                {status}
              </span>
            </div>
            <div className="mt-4 grid gap-2 text-sm text-muted-foreground">
              <span>
                <Trans>Toggle the calendars you want to check for conflicts to prevent double bookings.</Trans>
              </span>
              <label className="flex items-center gap-3">
                <input type="checkbox" defaultChecked={true} disabled />
                <span>colinswart0@gmail.com</span>
                <span className="rounded bg-primary/15 px-2 py-0.5 text-xs text-primary">
                  <Trans>Adding events to</Trans>
                </span>
              </label>
            </div>
          </div>
        </section>
      </div>
    </AppLayout>
  );
}

function SettingSelect({ label, value, help }: { label: string; value: string; help: string }) {
  return (
    <label className="grid gap-2">
      <span className="text-sm font-semibold">{label}</span>
      <select
        disabled
        value={value}
        className="h-10 rounded-md border border-input bg-background px-3 text-sm outline-none disabled:opacity-80"
      >
        <option>{value}</option>
      </select>
      <span className="text-xs leading-relaxed text-muted-foreground">{help}</span>
    </label>
  );
}

function GoogleCalendarLogo() {
  return (
    <div className="relative grid size-12 shrink-0 grid-cols-2 overflow-hidden rounded-lg text-xs font-bold text-white shadow-sm">
      <span className="bg-blue-500" />
      <span className="bg-yellow-400" />
      <span className="bg-green-500" />
      <span className="bg-red-500" />
      <span className="absolute top-3 left-3 text-lg text-white">31</span>
    </div>
  );
}

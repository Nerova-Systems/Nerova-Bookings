import { t } from "@lingui/core/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowLeftIcon, CheckIcon, Loader2Icon, RefreshCwIcon } from "lucide-react";
import { useEffect, useMemo } from "react";
import { toast } from "sonner";

import { useAppointmentShell } from "@/shared/lib/appointmentsApi";
import { useCreateIntegrationConnectSession, useSyncIntegrationConnections } from "@/shared/lib/integrationsApi";

import { AppLogo } from "./-components/AppLogo";
import { AppPreview } from "./-components/AppPreview";
import { findApp } from "./-components/appCatalog";

export const Route = createFileRoute("/dashboard/apps/$appSlug")({
  staticData: { trackingTitle: "App details" },
  component: AppDetailsPage
});

function AppDetailsPage() {
  const { appSlug } = Route.useParams();
  const app = findApp(appSlug);
  const shellQuery = useAppointmentShell();
  const connectSessionMutation = useCreateIntegrationConnectSession();
  const syncConnectionsMutation = useSyncIntegrationConnections();
  const googleCalendarIntegration = useMemo(
    () => shellQuery.data?.integrations.find((integration) => integration.provider === "Google" && integration.capability === "Calendar"),
    [shellQuery.data?.integrations]
  );

  useEffect(() => {
    document.title = app ? `${app.name} | Nerova` : t`App details | Nerova`;
  }, [app]);

  if (!app) {
    return (
      <main className="flex min-h-0 flex-1 flex-col overflow-y-auto bg-[#0f0f0f] px-8 py-8 text-white">
        <Link to="/dashboard/apps/store" className="inline-flex items-center gap-3 text-lg font-semibold text-white/85">
          <ArrowLeftIcon className="size-5" />
          App store
        </Link>
        <div className="mt-16 rounded-2xl border border-white/10 bg-[#202020] p-8">
          <h1 className="font-display text-3xl font-semibold">App not found</h1>
          <p className="mt-2 text-white/55">This app is not available in the catalog.</p>
        </div>
      </main>
    );
  }

  const appSlugForAction = app.slug;
  const usesNangoConnect = appSlugForAction === "google-calendar";
  const isInstalled = usesNangoConnect ? googleCalendarIntegration?.status === "Connected" : app.installState === "installed";
  const actionPending = connectSessionMutation.isPending || syncConnectionsMutation.isPending;

  async function handlePrimaryAction() {
    try {
      const session = await connectSessionMutation.mutateAsync({ appSlug: appSlugForAction });
      window.open(session.connectLink, "_blank", "noopener,noreferrer");
      toast.success("Finish Google Calendar authorization in the new tab, then refresh status here.");
    } catch (error) {
      toast.error(readError(error, "Could not start Google Calendar connection."));
    }
  }

  async function handleRefreshStatus() {
    try {
      await syncConnectionsMutation.mutateAsync({ appSlug: appSlugForAction });
      toast.success("Google Calendar connection status refreshed.");
    } catch (error) {
      toast.error(readError(error, "Could not refresh Google Calendar status."));
    }
  }

  return (
    <main className="flex min-h-0 flex-1 flex-col overflow-y-auto bg-[#0f0f0f] px-8 py-8 text-white">
      <Link to="/dashboard/apps/store" className="inline-flex w-fit items-center gap-3 text-lg font-semibold text-white/85 hover:text-white">
        <ArrowLeftIcon className="size-5" />
        App store
      </Link>

      <div className="mt-16 grid gap-12 xl:grid-cols-[minmax(28rem,54rem)_minmax(24rem,36rem)]">
        <section className="space-y-6">
          <AppPreview app={app} />
          <AppPreview app={app} />
        </section>

        <aside className="min-w-0">
          <div className="flex items-center gap-6">
            <AppLogo app={app} size="lg" />
            <h1 className="font-display text-5xl font-semibold leading-tight">{app.name}</h1>
          </div>

          <div className="mt-7 flex flex-wrap items-center gap-2 text-lg font-semibold text-white/75">
            <span className="rounded-full bg-white/10 px-2.5 py-1 text-base text-white">{app.category}</span>
            <span>Published by {app.publisher}</span>
          </div>

          <div className="mt-12 flex flex-wrap gap-4">
            {isInstalled && (
              <Button type="button" variant="outline" disabled className="border-white/10 bg-transparent text-white/50">
                <CheckIcon className="size-4" />1 active install
              </Button>
            )}
            {usesNangoConnect ? (
              <Button
                type="button"
                variant="outline"
                className="border-white/20 bg-transparent text-white hover:bg-white/10"
                disabled={actionPending}
                onClick={handlePrimaryAction}
              >
                {connectSessionMutation.isPending && <Loader2Icon className="size-4 animate-spin" />}
                {isInstalled ? "Reconnect" : "Install app"}
              </Button>
            ) : (
              <span className="inline-flex h-10 items-center rounded-xl border border-white/10 px-4 text-sm font-semibold text-white/45">
                Not available in MVP
              </span>
            )}
            {usesNangoConnect && (
              <Button
                type="button"
                variant="outline"
                className="border-white/20 bg-transparent text-white hover:bg-white/10"
                disabled={actionPending}
                onClick={handleRefreshStatus}
              >
                {syncConnectionsMutation.isPending ? <Loader2Icon className="size-4 animate-spin" /> : <RefreshCwIcon className="size-4" />}
                Refresh status
              </Button>
            )}
          </div>

          <p className="mt-12 text-xl leading-8 text-white/75">{app.description}</p>

          <section className="mt-12 space-y-8">
            <DetailBlock title="Pricing" value={app.pricing} />
            <DetailBlock title="Contact" value={app.support} />
            <div>
              <h2 className="text-xl font-semibold">Capabilities</h2>
              <div className="mt-3 flex flex-wrap gap-2">
                {app.capabilities.map((capability) => (
                  <span key={capability} className="rounded-full border border-white/10 px-3 py-1.5 text-sm text-white/70">
                    {capability}
                  </span>
                ))}
              </div>
            </div>
          </section>
        </aside>
      </div>
    </main>
  );
}

function DetailBlock({ title, value }: { title: string; value: string }) {
  return (
    <div>
      <h2 className="text-xl font-semibold">{title}</h2>
      <p className="mt-1 text-lg text-white/65">{value}</p>
    </div>
  );
}

function readError(error: unknown, fallback: string) {
  return error instanceof Error && error.message.trim().length > 0 ? error.message : fallback;
}

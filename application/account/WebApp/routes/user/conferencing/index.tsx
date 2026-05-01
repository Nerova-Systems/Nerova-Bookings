import { t } from "@lingui/core/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { createFileRoute } from "@tanstack/react-router";
import { VideoIcon } from "lucide-react";

import { useMainAppointmentShell } from "@/shared/lib/mainAppSettingsApi";

export const Route = createFileRoute("/user/conferencing/")({
  staticData: { trackingTitle: "Conferencing" },
  component: ConferencingPage
});

function ConferencingPage() {
  const shellQuery = useMainAppointmentShell();
  const googleCalendarConnected = shellQuery.data?.integrations.some(
    (integration) => integration.provider === "Google" && integration.capability === "Calendar" && integration.status === "Connected"
  );

  return (
    <AppLayout
      variant="center"
      maxWidth="64rem"
      balanceWidth="16rem"
      title={t`Conferencing`}
      subtitle={t`Add your favourite video conferencing apps for your meetings`}
    >
      <section className="mt-6 overflow-hidden rounded-xl border border-border bg-card">
        <ConferencingRow
          name="Cal Video"
          badge={t`Default`}
          description={t`Cal Video is the in-house web-based video conferencing platform powered by Daily.co.`}
          colorClassName="bg-slate-700"
        />
        <ConferencingRow
          name="Google Meet"
          badge={googleCalendarConnected ? t`Available through Google Calendar` : t`Connect Google Calendar`}
          description={t`Google Meet is Google's web-based video conferencing platform, designed to compete with major conferencing platforms.`}
          colorClassName="bg-emerald-600"
        />
      </section>
    </AppLayout>
  );
}

function ConferencingRow({
  name,
  badge,
  description,
  colorClassName
}: {
  name: string;
  badge?: string;
  description: string;
  colorClassName: string;
}) {
  return (
    <div className="flex items-center gap-4 border-b border-border px-5 py-4 last:border-b-0">
      <div className={`flex size-10 shrink-0 items-center justify-center rounded-lg ${colorClassName}`}>
        <VideoIcon className="size-5 text-white" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-semibold">{name}</span>
          {badge && <span className="rounded bg-emerald-500/15 px-2 py-0.5 text-xs font-semibold text-emerald-500">{badge}</span>}
        </div>
        <p className="mt-1 text-sm leading-relaxed text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}

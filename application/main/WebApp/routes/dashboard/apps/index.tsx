import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { createFileRoute } from "@tanstack/react-router";
import { PlugIcon } from "lucide-react";
import { useEffect } from "react";

import { useAppointmentShell, type IntegrationConnection } from "@/shared/lib/appointmentsApi";

export const Route = createFileRoute("/dashboard/apps/")({
  staticData: { trackingTitle: "Apps" },
  component: AppsPage
});

function AppsPage() {
  const shellQuery = useAppointmentShell();
  const integrations = shellQuery.data?.integrations ?? [];
  const grouped = groupIntegrations(integrations);

  useEffect(() => {
    document.title = t`Apps | Nerova`;
  }, []);

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <header className="sticky top-0 z-20 flex shrink-0 items-center gap-4 border-b border-border bg-background px-7 py-3.5">
        <div className="flex flex-col gap-0.5">
          <h1 className="font-display text-[1.375rem] leading-tight">
            <Trans>Apps</Trans>
          </h1>
          <span className="text-[12.5px] text-muted-foreground">
            <Trans>Google and Microsoft are priority-one integrations through Nango</Trans>
          </span>
        </div>
      </header>

      <main className="flex-1 overflow-y-auto px-7 py-6">
        <section className="mb-5 rounded-lg border border-border bg-background px-4 py-3">
          <div className="flex items-center gap-2">
            <PlugIcon className="size-4 text-muted-foreground" />
            <h2 className="font-display text-sm font-semibold">
              <Trans>Integration library</Trans>
            </h2>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            <Trans>
              These app records are the foundation for calendar busy blocks, contact imports, and email handoffs.
            </Trans>
          </p>
        </section>

        <section className="grid grid-cols-[repeat(auto-fill,minmax(18rem,1fr))] gap-3">
          {grouped.map((app) => (
            <article key={app.provider} className="rounded-lg border border-border bg-background p-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="font-display text-base font-semibold">{app.provider}</h3>
                  <p className="mt-1 text-xs text-muted-foreground">Priority one via Nango</p>
                </div>
                <span className="rounded-full bg-muted px-2 py-1 text-xs text-muted-foreground">{app.status}</span>
              </div>
              <div className="mt-4 flex flex-wrap gap-1.5">
                {app.capabilities.map((capability) => (
                  <span key={capability} className="rounded-full border border-border px-2 py-1 text-xs">
                    {capability}
                  </span>
                ))}
              </div>
            </article>
          ))}
        </section>
      </main>
    </div>
  );
}

function groupIntegrations(integrations: IntegrationConnection[]) {
  return Object.values(
    integrations.reduce<Record<string, { provider: string; status: string; capabilities: string[] }>>(
      (groups, integration) => {
        groups[integration.provider] ??= {
          provider: integration.provider,
          status: integration.status,
          capabilities: []
        };
        groups[integration.provider].capabilities.push(integration.capability);
        return groups;
      },
      {}
    )
  );
}

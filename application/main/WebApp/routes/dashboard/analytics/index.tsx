import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";

import { useAppointmentShell } from "@/shared/lib/appointmentsApi";

export const Route = createFileRoute("/dashboard/analytics/")({
  staticData: { trackingTitle: "Analytics" },
  component: AnalyticsPage
});

type Range = "7d" | "30d" | "90d" | "12m";

const RANGE_LABELS: Record<Range, string> = { "7d": "7 days", "30d": "30 days", "90d": "90 days", "12m": "12 months" };

const CHART_SVG = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 200" preserveAspectRatio="none"><defs><linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="%23242424" stop-opacity="0.18"/><stop offset="1" stop-color="%23242424" stop-opacity="0"/></linearGradient></defs><path d="M0 160 L20 150 L40 130 L60 140 L80 110 L100 120 L120 90 L140 100 L160 80 L180 95 L200 70 L220 85 L240 50 L260 70 L280 60 L300 80 L320 50 L340 65 L360 40 L380 55 L400 30 L420 50 L440 45 L460 35 L480 25 L500 40 L520 30 L540 20 L560 35 L580 15 L600 30 L600 200 L0 200 Z" fill="url(%23g)"/><path d="M0 160 L20 150 L40 130 L60 140 L80 110 L100 120 L120 90 L140 100 L160 80 L180 95 L200 70 L220 85 L240 50 L260 70 L280 60 L300 80 L320 50 L340 65 L360 40 L380 55 L400 30 L420 50 L440 45 L460 35 L480 25 L500 40 L520 30 L540 20 L560 35 L580 15 L600 30" stroke="%23242424" stroke-width="1.6" fill="none"/></svg>`;

function AnalyticsPage() {
  const [range, setRange] = useState<Range>("30d");
  const shellQuery = useAppointmentShell();
  const analytics = shellQuery.data?.analytics;
  const kpiStats = [
    { label: "Bookings", value: String(analytics?.bookings ?? 0) },
    { label: "Revenue", value: analytics?.revenue ?? "R 0" },
    { label: "Clients served", value: String(analytics?.clientsServed ?? 0) },
    { label: "Avg. booking value", value: analytics?.averageBookingValue ?? "R 0" },
    { label: "No-show rate", value: analytics?.noShowRate ?? "0%" }
  ];
  const bookingSources = buildBookingSources(shellQuery.data?.appointments.length ?? 0);
  const topServices = buildTopServices(shellQuery.data?.services ?? []);

  useEffect(() => {
    document.title = t`Analytics | Nerova`;
  }, []);

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <AnalyticsHeader />

      <div className="flex-1 overflow-y-auto px-7 py-6">
        <div className="mb-4.5 inline-flex gap-0.5 rounded-lg bg-muted p-0.5">
          {(Object.keys(RANGE_LABELS) as Range[]).map((r) => (
            <button
              key={r}
              type="button"
              onClick={() => setRange(r)}
              className={`rounded-md px-3 py-1.5 text-[12.5px] font-medium transition-colors ${
                range === r ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
              }`}
            >
              {RANGE_LABELS[r]}
            </button>
          ))}
        </div>

        <div className="mb-4.5 grid grid-cols-5 gap-2.5">
          {kpiStats.map((kpi) => (
            <div key={kpi.label} className="rounded-xl border border-border bg-background p-3.5">
              <div className="text-[11.5px] tracking-[0.05em] text-muted-foreground uppercase">{kpi.label}</div>
              <div className="mt-1.5 font-display text-[1.625rem] leading-none font-semibold">{kpi.value}</div>
              <div className="mt-1.5 font-mono text-[11px] text-muted-foreground">live database view</div>
            </div>
          ))}
        </div>

        <div className="mb-3 grid grid-cols-[2fr_1fr] gap-3">
          <div className="rounded-xl border border-border bg-background p-4">
            <div className="mb-3 flex items-baseline gap-2.5">
              <h3 className="font-display text-sm font-semibold">
                <Trans>Bookings over time</Trans>
              </h3>
              <span className="text-xs text-muted-foreground">
                <Trans>Daily</Trans>
              </span>
            </div>
            <div
              className="h-[12.5rem] rounded-md"
              style={{
                background: `url("data:image/svg+xml;utf8,${CHART_SVG}") center / 100% 100% no-repeat`
              }}
            />
          </div>

          <div className="rounded-xl border border-border bg-background p-4">
            <div className="mb-3 flex items-baseline gap-2.5">
              <h3 className="font-display text-sm font-semibold">
                <Trans>Booking sources</Trans>
              </h3>
            </div>
            <div className="space-y-3.5">
              {bookingSources.map((src) => (
                <div key={src.label} className="grid grid-cols-[1fr_auto] items-center gap-3">
                  <div>
                    <div className="mb-1 flex items-center justify-between text-xs">
                      <span className="text-foreground">{src.label}</span>
                      <span className="font-mono text-muted-foreground">{src.pct}%</span>
                    </div>
                    <div className="h-2 overflow-hidden rounded-full bg-muted">
                      <div className={`h-full rounded-full ${src.color}`} style={{ width: `${src.pct}%` }} />
                    </div>
                  </div>
                  <span className="font-mono text-xs whitespace-nowrap text-muted-foreground">{src.count}</span>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="rounded-xl border border-border bg-background p-4">
          <div className="mb-3 flex items-baseline gap-2.5">
            <h3 className="font-display text-sm font-semibold">
              <Trans>Top services</Trans>
            </h3>
            <span className="text-xs text-muted-foreground">
              <Trans>by bookings</Trans>
            </span>
          </div>
          <table className="w-full text-[12.5px]">
            <thead>
              <tr>
                {["Service", "Bookings", "Revenue", "Share"].map((h) => (
                  <th
                    key={h}
                    className="border-b border-border px-2 py-2 text-left text-[11px] font-semibold tracking-[0.05em] text-muted-foreground uppercase"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {topServices.map((row) => (
                <tr key={row.name} className="border-b border-border last:border-0">
                  <td className="px-2 py-2">{row.name}</td>
                  <td className="px-2 py-2 font-mono">{row.bookings}</td>
                  <td className="px-2 py-2 font-mono">{row.revenue}</td>
                  <td className="px-2 py-2">
                    <div className="flex items-center gap-2">
                      <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-muted">
                        <div className="h-full rounded-full bg-foreground" style={{ width: `${row.fill}%` }} />
                      </div>
                      <span className="font-mono text-[11px] text-muted-foreground">{row.fill}%</span>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function AnalyticsHeader() {
  return (
    <header className="sticky top-0 z-20 flex flex-shrink-0 items-center gap-4 border-b border-border bg-background px-7 py-3.5">
      <div className="flex flex-col gap-0.5">
        <h1 className="font-display text-[1.375rem] leading-tight">
          <Trans>Analytics</Trans>
        </h1>
        <span className="text-[12.5px] text-muted-foreground">
          <Trans>Operational metrics · last 30 days</Trans>
        </span>
      </div>
      <div className="ml-auto flex items-center gap-2">
        <Button variant="outline" size="sm">
          <Trans>Compare ranges</Trans>
        </Button>
        <Button size="sm">
          <Trans>Export CSV</Trans>
        </Button>
      </div>
    </header>
  );
}

function buildBookingSources(total: number) {
  const publicCount = total;
  return [
    {
      label: "Public booking page",
      pct: total === 0 ? 0 : 100,
      color: "bg-foreground",
      count: `${publicCount} bookings`
    },
    { label: "Manual booking", pct: 0, color: "bg-muted-foreground/50", count: "0 bookings" },
    { label: "Future WhatsApp fixed flow", pct: 0, color: "bg-success", count: "deferred" }
  ];
}

function buildTopServices(services: { name: string; bookingsThisMonth: number; price: string }[]) {
  const maxBookings = Math.max(1, ...services.map((service) => service.bookingsThisMonth));
  return services.slice(0, 5).map((service) => ({
    name: service.name,
    bookings: service.bookingsThisMonth,
    revenue: service.price,
    fill: Math.round((service.bookingsThisMonth / maxBookings) * 100)
  }));
}

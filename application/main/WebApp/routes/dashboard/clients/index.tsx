import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { SearchIcon } from "lucide-react";
import { useEffect, useState } from "react";

import { useAppointmentShell } from "@/shared/lib/appointmentsApi";

import { FILTER_LABELS, FlagDot, type Filter } from "./-components/clientData";

export const Route = createFileRoute("/dashboard/clients/")({
  staticData: { trackingTitle: "Clients" },
  component: ClientsPage
});

function ClientsPage() {
  const [search, setSearch] = useState("");
  const [activeFilter, setActiveFilter] = useState<Filter>("all");
  const shellQuery = useAppointmentShell();
  const clients = shellQuery.data?.clients ?? [];
  const filterCounts = {
    all: clients.length,
    vip: clients.filter((client) => client.status === "VIP").length,
    new: clients.filter((client) => client.status === "New").length,
    blocked: clients.filter((client) => client.status === "Blocked").length
  };

  useEffect(() => {
    document.title = t`Clients | Nerova`;
  }, []);

  const filtered = clients.filter((c) => {
    const matchesSearch =
      search === "" || c.name.toLowerCase().includes(search.toLowerCase()) || c.phone.includes(search);
    const matchesFilter =
      activeFilter === "all" ||
      (activeFilter === "vip" && c.status === "VIP") ||
      (activeFilter === "new" && c.status === "New") ||
      (activeFilter === "blocked" && c.status === "Blocked");
    return matchesSearch && matchesFilter;
  });

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <header className="sticky top-0 z-20 flex shrink-0 items-center gap-4 border-b border-border bg-background px-7 py-3.5">
        <div className="flex flex-col gap-0.5">
          <h1 className="font-display text-[1.375rem] leading-tight">
            <Trans>Clients</Trans>
          </h1>
          <span className="text-[12.5px] text-muted-foreground">
            {clients.length} clients · {clients.filter((client) => client.status === "New").length} new
          </span>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Button variant="outline" size="sm">
            <Trans>Import contacts</Trans>
          </Button>
          <Button size="sm">
            <Trans>Add client</Trans>
          </Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto px-7 py-6">
        <div className="mb-4 flex flex-wrap items-center gap-3.5">
          <div className="flex w-80 items-center gap-2 rounded-lg bg-muted px-3 py-1.5">
            <SearchIcon className="size-3.5 shrink-0 text-muted-foreground" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t`Search clients…`}
              className="flex-1 border-0 bg-transparent font-sans text-[0.8125rem] text-foreground outline-none placeholder:text-muted-foreground"
            />
          </div>
          <div className="flex flex-wrap gap-1.5">
            {(Object.keys(FILTER_LABELS) as Filter[]).map((f) => (
              <button
                key={f}
                type="button"
                onClick={() => setActiveFilter(f)}
                className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs transition-colors ${
                  activeFilter === f
                    ? "border-foreground bg-foreground text-background"
                    : "border-border bg-background text-foreground hover:border-foreground/30"
                }`}
              >
                {FILTER_LABELS[f]}
                <span
                  className={`rounded-full px-1.5 text-[10.5px] ${activeFilter === f ? "bg-background/20" : "bg-muted text-muted-foreground"}`}
                >
                  {filterCounts[f]}
                </span>
              </button>
            ))}
          </div>
        </div>

        <div className="overflow-hidden rounded-xl border border-border bg-background">
          <div className="grid grid-cols-[1.6fr_1.2fr_0.5fr_0.7fr_0.9fr_0.5fr_2rem] items-center gap-3 bg-muted px-4 py-2.5 text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            <span>Client</span>
            <span>Phone</span>
            <span>Visits</span>
            <span>Lifetime</span>
            <span>Last visit</span>
            <span>Status</span>
            <span />
          </div>
          {filtered.map((client) => (
            <div
              key={client.name}
              className={`grid cursor-pointer grid-cols-[1.6fr_1.2fr_0.5fr_0.7fr_0.9fr_0.5fr_2rem] items-center gap-3 border-t border-border px-4 py-2.5 text-[0.8125rem] text-foreground transition-colors hover:bg-muted ${
                client.status === "Blocked" ? "opacity-70" : ""
              }`}
            >
              <div className="flex min-w-0 items-center gap-2.5">
                <div
                  className={`flex size-7.5 shrink-0 items-center justify-center rounded-full font-sans text-[11px] font-semibold ${client.status === "Blocked" ? "bg-muted-foreground/30 text-muted-foreground" : "bg-foreground text-background"}`}
                >
                  {client.initials}
                </div>
                <span className="truncate font-medium">{client.name}</span>
              </div>
              <span className="font-mono text-[0.8rem]">{client.phone}</span>
              <span>{client.visits}</span>
              <span className="font-mono">{client.lifetime}</span>
              <span>{client.lastVisit}</span>
              <span className="text-[12px] text-muted-foreground">{client.status}</span>
              <div className="flex items-center justify-center">
                <FlagDot flag={client.flag} />
              </div>
            </div>
          ))}
          {filtered.length === 0 && (
            <div className="border-t border-border px-4 py-10 text-center text-sm text-muted-foreground">
              <Trans>No clients match your search.</Trans>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { SearchIcon } from "lucide-react";
import { useEffect, useState } from "react";

import { AppointmentDetail } from "./-components/AppointmentDetail";
import { AppointmentList, type FilterTab } from "./-components/AppointmentList";
import { APPOINTMENTS } from "./-components/appointmentTypes";
import { ClientPanel } from "./-components/ClientPanel";
import { CommandPalette } from "./-components/CommandPalette";

export const Route = createFileRoute("/dashboard/")({
  staticData: { trackingTitle: "Activity" },
  component: ActivityPage
});

function ActivityPage() {
  const [selectedId, setSelectedId] = useState("1");
  const [activeTab, setActiveTab] = useState<FilterTab>("all");
  const [cmdkOpen, setCmdkOpen] = useState(false);

  const selectedAppointment = APPOINTMENTS.find((a) => a.id === selectedId) ?? APPOINTMENTS[0];

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setCmdkOpen(true);
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, []);

  useEffect(() => {
    document.title = t`Activity | Nerova`;
  }, []);

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <header className="sticky top-0 z-20 flex shrink-0 items-center gap-4 border-b border-border bg-background px-7 py-3.5">
        <div className="flex min-w-0 flex-col gap-0.5">
          <h1 className="font-display text-[1.375rem] leading-tight">
            <Trans>Activity</Trans>
          </h1>
          <span className="text-[12.5px] text-muted-foreground">
            <Trans>Operational feed · 7 items need review</Trans>
          </span>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <button
            type="button"
            onClick={() => setCmdkOpen(true)}
            className="flex w-65 items-center gap-2 rounded-lg border border-transparent bg-muted px-2.5 py-1.5 text-[12.5px] text-muted-foreground transition-colors hover:border-border"
          >
            <SearchIcon className="size-3.5 shrink-0" />
            <span className="flex-1 text-left">
              <Trans>Search anything</Trans>
            </span>
            <kbd className="rounded border border-border bg-background px-1.5 py-0.5 font-sans text-[10.5px] font-medium text-foreground">
              ⌘K
            </kbd>
          </button>
          <Button variant="outline" size="sm">
            <Trans>View calendar</Trans>
          </Button>
          <Button size="sm">
            <Trans>New manual booking</Trans>
          </Button>
        </div>
      </header>

      <div className="grid min-h-0 flex-1 grid-cols-[380px_1fr_320px] overflow-hidden">
        <AppointmentList
          selectedId={selectedId}
          onSelect={setSelectedId}
          activeTab={activeTab}
          onTabChange={setActiveTab}
        />
        <AppointmentDetail appointment={selectedAppointment} />
        <ClientPanel appointment={selectedAppointment} />
      </div>

      <CommandPalette open={cmdkOpen} onOpenChange={setCmdkOpen} />
    </div>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useEffect } from "react";

import { WeekGrid } from "./-components/WeekGrid";

export const Route = createFileRoute("/dashboard/calendar/")({
  staticData: { trackingTitle: "Calendar" },
  component: CalendarPage
});

function CalendarPage() {
  useEffect(() => {
    document.title = t`Calendar | Nerova`;
  }, []);

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <header className="sticky top-0 z-20 flex shrink-0 items-center gap-4 border-b border-border bg-background px-7 py-3.5">
        <div className="flex flex-col gap-0.5">
          <h1 className="font-display text-[1.375rem] leading-tight">
            <Trans>Calendar</Trans>
          </h1>
          <span className="text-[12.5px] text-muted-foreground">
            <Trans>Week view · Africa/Johannesburg</Trans>
          </span>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Button variant="outline" size="sm">
            <Trans>Block time</Trans>
          </Button>
          <Button size="sm">
            <Trans>New manual booking</Trans>
          </Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto px-7 py-6">
        <div className="mb-3.5 flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm">
              <Trans>Today</Trans>
            </Button>
            <div className="flex gap-0.5">
              <button
                type="button"
                className="flex size-7 items-center justify-center rounded-md border border-border bg-transparent text-foreground hover:bg-muted"
              >
                <ChevronLeftIcon className="size-3" />
              </button>
              <button
                type="button"
                className="flex size-7 items-center justify-center rounded-md border border-border bg-transparent text-foreground hover:bg-muted"
              >
                <ChevronRightIcon className="size-3" />
              </button>
            </div>
            <span className="pl-1.5 font-display text-base font-semibold">22 — 28 April 2026</span>
          </div>
          <div className="ml-auto flex items-center gap-2">
            <div className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-1 text-xs text-muted-foreground">
              <span className="size-1.5 rounded-full bg-success shadow-[0_0_0_3px_rgba(44,122,79,0.2)]" />
              <Trans>Synced via Google · 6 busy blocks imported</Trans>
            </div>
            <div className="inline-flex gap-0.5 rounded-lg bg-muted p-0.5">
              {(["Day", "Week", "Month"] as const).map((v) => (
                <button
                  key={v}
                  type="button"
                  className={`rounded-md px-2.5 py-1 text-xs font-medium transition-colors ${
                    v === "Week" ? "bg-background text-foreground shadow-sm" : "text-muted-foreground"
                  }`}
                >
                  {v}
                </button>
              ))}
            </div>
          </div>
        </div>

        <WeekGrid />
      </div>
    </div>
  );
}

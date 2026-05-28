import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Link as RouterLink } from "@tanstack/react-router";
import { BlocksIcon } from "lucide-react";

import { AppCategory, api } from "@/shared/lib/api/client";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeAppsTab(_props: EventTypeTabProps) {
  // Per-event-type app configuration endpoint (e.g. selecting which calendar a booking writes
  // to per event type) does not exist on the backend yet — only tenant/user-level installation
  // is wired up via /api/apps. This tab therefore surfaces a read-only summary of what the user
  // has connected and points to the user-level Installed Apps page for install/uninstall.
  // Deferred: GET/PUT /api/event-types/{id}/apps for per-event-type calendar destination and
  // payment app configuration.
  const { data, isLoading } = api.useQuery("get", "/api/apps");
  const apps = data?.apps ?? [];
  const calendarApps = apps.filter((app) => app.category === AppCategory.Calendar && app.isConnectedForUser);
  const conferencingApps = apps.filter((app) => app.category === AppCategory.Conferencing && app.isConnectedForUser);
  const hasAnyConnected = calendarApps.length > 0 || conferencingApps.length > 0;

  return (
    <div className="grid gap-5">
      <EventTypeTabSection
        title={<Trans>Apps</Trans>}
        description={
          <Trans>
            Apps installed for your account are available to this event type. Conferencing apps appear as location
            choices on the Setup tab, and connected calendars are used to check availability.
          </Trans>
        }
      >
        {isLoading ? (
          <div className="rounded-md border p-4 text-sm text-muted-foreground">
            <Trans>Loading apps...</Trans>
          </div>
        ) : !hasAnyConnected ? (
          <Empty className="min-h-40 border">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <BlocksIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No apps installed</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>Install a calendar or conferencing app to use it with this event type.</Trans>
              </EmptyDescription>
            </EmptyHeader>
            <Button render={<RouterLink to="/apps/installed" />}>
              <Trans>Manage apps</Trans>
            </Button>
          </Empty>
        ) : (
          <>
            <InstalledAppGroup
              label={t`Conferencing`}
              emptyMessage={t`No conferencing apps connected. Install one to add it as a location.`}
              apps={conferencingApps}
            />
            <InstalledAppGroup
              label={t`Connected calendars`}
              emptyMessage={t`No calendars connected. Install one so availability checks include external events.`}
              apps={calendarApps}
              footnote={
                <Trans>
                  Per-event-type calendar destination is not yet configurable — connected calendars are used for
                  availability only.
                </Trans>
              }
            />
            <div className="flex justify-end">
              <Button variant="outline" size="sm" render={<RouterLink to="/apps/installed" />}>
                <Trans>Manage apps</Trans>
              </Button>
            </div>
          </>
        )}
      </EventTypeTabSection>
    </div>
  );
}

function InstalledAppGroup({
  label,
  emptyMessage,
  apps,
  footnote
}: Readonly<{
  label: string;
  emptyMessage: string;
  apps: Array<{ slug: string; name: string; description: string; logoUrl: string }>;
  footnote?: React.ReactNode;
}>) {
  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-muted-foreground">{label}</div>
      {apps.length === 0 ? (
        <div className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{emptyMessage}</div>
      ) : (
        <div className="overflow-hidden rounded-md border">
          {apps.map((app) => (
            <div key={app.slug} className="flex items-center gap-3 border-b p-3 last:border-b-0">
              {app.logoUrl ? (
                <img
                  src={app.logoUrl}
                  alt=""
                  className="h-8 w-8 shrink-0 rounded border bg-background object-contain p-1"
                />
              ) : (
                <div
                  aria-hidden="true"
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded border bg-muted text-xs font-semibold text-muted-foreground"
                >
                  {app.name.slice(0, 1).toUpperCase()}
                </div>
              )}
              <div className="min-w-0 flex-1">
                <div className="font-medium">{app.name}</div>
                {app.description && <div className="truncate text-sm text-muted-foreground">{app.description}</div>}
              </div>
              <Badge>
                <Trans>Connected</Trans>
              </Badge>
            </div>
          ))}
        </div>
      )}
      {footnote && <div className="text-xs text-muted-foreground">{footnote}</div>}
    </div>
  );
}

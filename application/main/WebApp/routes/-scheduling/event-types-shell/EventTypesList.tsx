import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { ButtonGroup } from "@repo/ui/components/ButtonGroup";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Input } from "@repo/ui/components/Input";
import { Switch } from "@repo/ui/components/Switch";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { Link as RouterLink } from "@tanstack/react-router";
import { SearchIcon, TimerIcon } from "lucide-react";
import { useMemo, useState } from "react";

import type { EventType, Schedule } from "../schedulingTypes";

import { EventTypeOverflowActions, PreviewEventTypeButton } from "./EventTypeActionButtons";
import { getEventTypePublicUrl, getScheduleName } from "./eventTypeShellTypes";

export function EventTypesList({
  eventTypes,
  schedules,
  publicHandle,
  isLoading,
  onDuplicate,
  onDelete,
  onHiddenChange
}: Readonly<{
  eventTypes: EventType[];
  schedules: Schedule[];
  publicHandle?: string | null;
  isLoading: boolean;
  onDuplicate: (eventType: EventType) => void;
  onDelete: (eventType: EventType) => void;
  onHiddenChange: (eventType: EventType, hidden: boolean) => void;
}>) {
  const [search, setSearch] = useState("");
  const filteredEventTypes = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    if (normalizedSearch.length === 0) return eventTypes;

    return eventTypes.filter((eventType) =>
      [eventType.title, eventType.slug, eventType.description ?? ""].some((value) =>
        value.toLowerCase().includes(normalizedSearch)
      )
    );
  }, [eventTypes, search]);

  return (
    <section className="flex min-w-0 flex-col gap-4">
      <div className="relative w-full sm:ml-auto sm:max-w-72">
        <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          aria-label={t`Search`}
          className="pl-9"
          placeholder={t`Search`}
          value={search}
          onChange={(event) => setSearch(event.currentTarget.value)}
        />
      </div>
      {isLoading ? (
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>Loading event types...</Trans>
        </div>
      ) : eventTypes.length === 0 ? (
        <Empty className="min-h-48 border">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <TimerIcon />
            </EmptyMedia>
            <EmptyTitle>
              <Trans>No event types yet</Trans>
            </EmptyTitle>
            <EmptyDescription>
              <Trans>Create a private setup event type before opening public booking.</Trans>
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : filteredEventTypes.length === 0 ? (
        <div className="rounded-md border p-8 text-center text-sm text-muted-foreground">
          <Trans>No event types match your search.</Trans>
        </div>
      ) : (
        <div className="overflow-hidden rounded-lg border bg-background">
          {filteredEventTypes.map((eventType) => (
            <article
              key={eventType.id}
              className="grid gap-3 border-b px-4 py-5 transition-colors last:border-b-0 hover:bg-muted/40 sm:px-6 md:grid-cols-[1fr_auto] md:items-center"
            >
              <RouterLink
                to="/event-types/$eventTypeId"
                params={{ eventTypeId: eventType.id }}
                search={{ tabName: "setup" }}
                className="min-w-0 rounded-md outline-ring transition-colors hover:text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1">
                  <h2 className="truncate text-base font-semibold">{eventType.title}</h2>
                  <span className="truncate text-sm text-muted-foreground">
                    {getEventTypePublicUrl(eventType, publicHandle)}
                  </span>
                </div>
                <div className="mt-3 flex flex-wrap items-center gap-2">
                  <Badge variant="outline" className="gap-1">
                    <TimerIcon className="size-3" />
                    {eventType.durationMinutes}m
                  </Badge>
                  <span className="text-xs text-muted-foreground">
                    {getScheduleName(eventType.scheduleId, schedules)}
                  </span>
                </div>
              </RouterLink>
              <div className="flex items-center justify-end gap-3">
                <Tooltip>
                  <TooltipTrigger
                    render={
                      <div className="flex min-h-[var(--control-height)] items-center">
                        <Switch
                          aria-label={
                            eventType.hidden ? t`Show event type on profile` : t`Hide event type from profile`
                          }
                          checked={!eventType.hidden}
                          onCheckedChange={(checked) => onHiddenChange(eventType, !checked)}
                        />
                      </div>
                    }
                  />
                  <TooltipContent>
                    {eventType.hidden ? (
                      <Trans>Show event type on profile</Trans>
                    ) : (
                      <Trans>Hide event type from profile</Trans>
                    )}
                  </TooltipContent>
                </Tooltip>
                <ButtonGroup className="hidden sm:flex">
                  <PreviewEventTypeButton eventType={eventType} publicHandle={publicHandle} />
                  <EventTypeOverflowActions
                    eventType={eventType}
                    publicHandle={publicHandle}
                    onDuplicate={() => onDuplicate(eventType)}
                    onDelete={() => onDelete(eventType)}
                  />
                </ButtonGroup>
                <div className="sm:hidden">
                  <EventTypeOverflowActions
                    eventType={eventType}
                    publicHandle={publicHandle}
                    onDuplicate={() => onDuplicate(eventType)}
                    onDelete={() => onDelete(eventType)}
                  />
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

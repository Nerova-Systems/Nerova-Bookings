import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Input } from "@repo/ui/components/Input";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { Link as RouterLink } from "@tanstack/react-router";
import { EyeOffIcon, FilesIcon, SearchIcon, TimerIcon, Trash2Icon } from "lucide-react";
import { useMemo, useState } from "react";

import type { EventType, Schedule } from "../schedulingTypes";
import { CopyEventTypeButton, EventTypeOverflowActions, PreviewEventTypeButton } from "./EventTypeActionButtons";
import { getEventTypePublicUrl, getScheduleName } from "./eventTypeShellTypes";

export function EventTypesList({
  eventTypes,
  schedules,
  isLoading,
  onDuplicate,
  onDelete
}: Readonly<{
  eventTypes: EventType[];
  schedules: Schedule[];
  isLoading: boolean;
  onDuplicate: (eventType: EventType) => void;
  onDelete: (eventType: EventType) => void;
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
      <div className="relative max-w-xl">
        <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          aria-label={t`Search event types`}
          className="pl-9"
          placeholder={t`Search event types`}
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
        <div className="overflow-hidden rounded-md border bg-background">
          {filteredEventTypes.map((eventType) => (
            <article
              key={eventType.id}
              className="grid gap-3 border-b p-4 last:border-b-0 md:grid-cols-[1fr_auto] md:items-center"
            >
              <RouterLink
                to="/event-types/$eventTypeId"
                params={{ eventTypeId: eventType.id }}
                search={{ tabName: "setup" }}
                className="min-w-0 rounded-md outline-ring transition-colors hover:text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="truncate text-base font-medium">{eventType.title}</h2>
                  <Badge variant="secondary">
                    <Trans>{eventType.durationMinutes} min</Trans>
                  </Badge>
                  {eventType.hidden && (
                    <Badge variant="outline">
                      <EyeOffIcon />
                      <Trans>Hidden</Trans>
                    </Badge>
                  )}
                </div>
                <p className="mt-1 truncate text-sm text-muted-foreground">{getEventTypePublicUrl(eventType)}</p>
                <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                  <span>{getScheduleName(eventType.scheduleId, schedules)}</span>
                  <span>/{eventType.slug}</span>
                </div>
              </RouterLink>
              <div className="flex items-center justify-end gap-1">
                <div className="hidden items-center gap-1 sm:flex">
                  <CopyEventTypeButton eventType={eventType} />
                  <PreviewEventTypeButton eventType={eventType} />
                  <Tooltip>
                    <TooltipTrigger
                      render={
                        <Button type="button" variant="ghost" size="icon-sm" onClick={() => onDuplicate(eventType)}>
                          <FilesIcon />
                          <span className="sr-only">
                            <Trans>Duplicate event type</Trans>
                          </span>
                        </Button>
                      }
                    />
                    <TooltipContent>
                      <Trans>Duplicate event type</Trans>
                    </TooltipContent>
                  </Tooltip>
                  <Tooltip>
                    <TooltipTrigger
                      render={
                        <Button type="button" variant="ghost" size="icon-sm" onClick={() => onDelete(eventType)}>
                          <Trash2Icon />
                          <span className="sr-only">
                            <Trans>Delete event type</Trans>
                          </span>
                        </Button>
                      }
                    />
                    <TooltipContent>
                      <Trans>Delete event type</Trans>
                    </TooltipContent>
                  </Tooltip>
                </div>
                <div className="sm:hidden">
                  <EventTypeOverflowActions
                    eventType={eventType}
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

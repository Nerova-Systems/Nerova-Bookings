import type { ReactNode } from "react";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { CalendarIcon, ClockIcon, GlobeIcon, MapPinIcon, UserIcon } from "lucide-react";

import type { PublicEventType } from "./publicBookerTypes";

import { formatMinutes } from "../../-scheduling/schedulingTypes";

export function EventMeta({
  eventType,
  handle,
  eventSlug,
  timezone
}: Readonly<{ eventType: PublicEventType; handle: string; eventSlug: string; timezone: string }>) {
  const location = eventType.locations?.[0] ?? { type: eventType.locationType, value: eventType.locationValue };

  return (
    <section className="relative z-10 flex flex-col gap-5 p-6 md:w-(--booker-meta-width)" data-testid="booker-event-meta">
      <div className="flex items-center gap-3">
        <div className="flex size-12 items-center justify-center rounded-full bg-primary text-primary-foreground">
          <UserIcon className="size-5" />
        </div>
        <div className="min-w-0">
          <span className="block truncate text-sm font-medium">{eventType.profile?.displayName ?? `@${handle}`}</span>
          <span className="block truncate text-xs text-muted-foreground">/{eventSlug}</span>
        </div>
      </div>
      <div className="flex flex-col gap-2">
        <Badge variant="outline" className="w-fit">
          <Trans>Public booking</Trans>
        </Badge>
        <h1>{eventType.title}</h1>
        {eventType.description && (
          <span className="text-sm leading-6 text-muted-foreground">{eventType.description}</span>
        )}
      </div>
      <div className="flex flex-col gap-3 text-sm text-muted-foreground">
        <MetaRow icon={<ClockIcon />} text={formatMinutes(eventType.durationMinutes)} />
        <MetaRow icon={<CalendarIcon />} text={t`One-on-one`} />
        <MetaRow icon={<GlobeIcon />} text={timezone} />
        {location?.type && <MetaRow icon={<MapPinIcon />} text={formatLocation(location.type, location.value)} />}
      </div>
    </section>
  );
}

function MetaRow({ icon, text }: Readonly<{ icon: ReactNode; text: string }>) {
  return (
    <span className="flex items-center gap-2">
      <span className="[&_svg]:size-4">{icon}</span>
      <span>{text}</span>
    </span>
  );
}

function formatLocation(type: string, value: string | null | undefined) {
  if (type === "link") return value || t`Video call`;
  if (type === "phone") return t`Phone call`;
  if (type === "inPerson" || type === "in-person") return value || t`In person`;
  return value || type;
}

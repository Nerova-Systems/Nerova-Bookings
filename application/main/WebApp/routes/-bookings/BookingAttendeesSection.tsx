import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { MailIcon, UserIcon } from "lucide-react";

import type { Schemas } from "@/shared/lib/api/client";

import { DetailRow, SectionTitle } from "./BookingDetailsSheetParts";

type BookingAttendee = Schemas["BookingAttendeeResponse"];

export function BookingAttendeesSection({
  isLoading,
  attendees,
  fallbackName,
  fallbackEmail
}: Readonly<{
  isLoading: boolean;
  attendees: BookingAttendee[] | undefined;
  fallbackName: string;
  fallbackEmail: string;
}>) {
  return (
    <section className="rounded-md border p-4">
      <SectionTitle>
        <Trans>Attendees</Trans>
      </SectionTitle>
      {isLoading ? (
        <Skeleton className="h-10 w-full" />
      ) : attendees && attendees.length > 0 ? (
        <ul className="flex flex-col gap-3">
          {attendees.map((attendee) => (
            <li key={attendee.id} className="flex flex-col gap-1">
              <div className="flex items-center gap-2">
                <UserIcon className="size-4 text-muted-foreground" />
                <span className="text-sm font-medium">{attendee.name}</span>
                {attendee.noShow && (
                  <Badge variant="destructive">
                    <Trans>No-show</Trans>
                  </Badge>
                )}
              </div>
              <span className="inline-flex items-center gap-2 text-xs text-muted-foreground">
                <MailIcon className="size-3.5" />
                {attendee.email}
              </span>
              <span className="text-xs text-muted-foreground">{attendee.timeZone}</span>
            </li>
          ))}
        </ul>
      ) : (
        <div className="grid gap-3">
          <DetailRow icon={<UserIcon />} label={<Trans>Name</Trans>}>
            {fallbackName}
          </DetailRow>
          <DetailRow icon={<MailIcon />} label={<Trans>Email</Trans>}>
            {fallbackEmail}
          </DetailRow>
        </div>
      )}
    </section>
  );
}

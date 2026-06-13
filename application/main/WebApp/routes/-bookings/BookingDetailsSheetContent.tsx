import type React from "react";

import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { SheetDescription, SheetFooter, SheetHeader, SheetTitle } from "@repo/ui/components/Sheet";
import { CalendarClockIcon, ClockIcon, LinkIcon, MailIcon, UserIcon } from "lucide-react";

import { BookingActionsDropdown } from "./BookingActionsDropdown";
import { type BookingListItem, formatBookingDateRange, getStatusVariant } from "./bookingTypes";

export function BookingDetailsHeader({ booking }: Readonly<{ booking: BookingListItem }>) {
  return (
    <SheetHeader className="border-b pr-12">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="mb-3 flex flex-wrap gap-2">
            <Badge variant={getStatusVariant(booking.status)}>{booking.status}</Badge>
            {booking.rescheduled && (
              <Badge variant="destructive">
                <Trans>Rescheduled</Trans>
              </Badge>
            )}
            {booking.isRecurring && (
              <Badge variant="outline">
                <Trans>Recurring</Trans>
              </Badge>
            )}
          </div>
          <SheetTitle className="text-xl font-semibold">{booking.eventTypeTitle}</SheetTitle>
          <SheetDescription>{formatBookingDateRange(booking)}</SheetDescription>
        </div>
      </div>
    </SheetHeader>
  );
}

export function BookingDetailsBody({ booking }: Readonly<{ booking: BookingListItem }>) {
  return (
    <div className="flex flex-1 flex-col gap-5 overflow-y-auto px-4 py-5">
      <BookingMetaSection booking={booking} />
      <ClientsSection booking={booking} />
      <BookingResponsesSection booking={booking} />
      <RescheduleSection booking={booking} />
      {booking.rejectionReason && (
        <section className="grid gap-3">
          <SectionTitle>
            <Trans>Rejection</Trans>
          </SectionTitle>
          <span className="text-sm text-muted-foreground">{booking.rejectionReason}</span>
        </section>
      )}
      <section className="grid gap-1">
        <SectionTitle>
          <Trans>System</Trans>
        </SectionTitle>
        <span className="text-xs text-muted-foreground">
          <Trans>Reference number</Trans>
        </span>
        <span className="mt-1 block text-sm break-all text-muted-foreground">{booking.id}</span>
      </section>
    </div>
  );
}

export function BookingDetailsFooter({
  booking,
  onActionComplete
}: Readonly<{ booking: BookingListItem; onActionComplete: () => void }>) {
  return (
    <SheetFooter className="border-t bg-muted/40 px-4 py-4 sm:px-6">
      <div className="flex w-full min-w-0 flex-wrap items-center justify-end gap-2">
        {booking.locationValue && !["cancelled", "rejected"].includes(booking.status.toLowerCase()) ? (
          <Button type="button" variant="secondary" size="sm">
            <LinkIcon />
            <Trans>Join meeting</Trans>
          </Button>
        ) : null}
        <BookingActionsDropdown booking={booking} onActionComplete={onActionComplete} />
      </div>
    </SheetFooter>
  );
}

function BookingMetaSection({ booking }: Readonly<{ booking: BookingListItem }>) {
  return (
    <section className="grid gap-4">
      <div className="grid gap-4">
        <DetailRow icon={<CalendarClockIcon />} label={<Trans>When</Trans>}>
          {formatBookingDateRange(booking)}
        </DetailRow>
        <DetailRow icon={<ClockIcon />} label={<Trans>Timezone</Trans>}>
          {booking.timeZone}
        </DetailRow>
        {(booking.locationValue || booking.locations.length > 0) && (
          <DetailRow icon={<LinkIcon />} label={<Trans>Location</Trans>}>
            {booking.locationValue ?? booking.locations.map((location) => location.value ?? location.type).join(", ")}
          </DetailRow>
        )}
      </div>
    </section>
  );
}

function ClientsSection({ booking }: Readonly<{ booking: BookingListItem }>) {
  return (
    <section className="grid gap-4">
      <SectionTitle>
        <Trans>Clients</Trans>
      </SectionTitle>
      <div className="grid gap-4">
        <DetailRow icon={<UserIcon />} label={<Trans>Name</Trans>}>
          {booking.bookerName}
        </DetailRow>
        <DetailRow icon={<MailIcon />} label={<Trans>Email</Trans>}>
          {booking.bookerEmail}
        </DetailRow>
        {booking.attendees.map((attendee) => (
          <DetailRow key={attendee.email} icon={<UserIcon />} label={<Trans>Guest</Trans>}>
            {attendee.name} · {attendee.email}
          </DetailRow>
        ))}
      </div>
    </section>
  );
}

function BookingResponsesSection({ booking }: Readonly<{ booking: BookingListItem }>) {
  if (!booking.description && Object.entries(booking.responses).length === 0) return null;

  return (
    <section className="grid gap-4">
      <SectionTitle>
        <Trans>Booking details</Trans>
      </SectionTitle>
      {booking.description && (
        <div className="mb-4 flex flex-col gap-1">
          <span className="text-xs text-muted-foreground">
            <Trans>Description</Trans>
          </span>
          <span className="text-sm text-muted-foreground">{booking.description}</span>
        </div>
      )}
      {Object.entries(booking.responses).length === 0 ? (
        <span className="text-sm text-muted-foreground">
          <Trans>No custom responses were captured.</Trans>
        </span>
      ) : (
        <div className="rounded-xl border">
          {Object.entries(booking.responses).map(([name, value]) => (
            <div key={name} className="grid gap-1 border-b p-3 last:border-b-0">
              <span className="text-xs text-muted-foreground">{name}</span>
              <span className="text-sm">{value}</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function RescheduleSection({ booking }: Readonly<{ booking: BookingListItem }>) {
  if (!booking.rescheduled && !booking.rescheduleReason && !booking.rescheduledBy) return null;

  return (
    <section className="grid gap-3">
      <SectionTitle>
        <Trans>Reschedule</Trans>
      </SectionTitle>
      <div className="grid gap-3 text-sm text-muted-foreground">
        {booking.rescheduled && (
          <span>
            <Trans>This booking was marked for reschedule.</Trans>
          </span>
        )}
        {booking.rescheduledBy && (
          <span>
            <Trans>By</Trans>: {booking.rescheduledBy}
          </span>
        )}
        {booking.rescheduleReason && (
          <span>
            <Trans>Reason</Trans>: {booking.rescheduleReason}
          </span>
        )}
      </div>
    </section>
  );
}

function SectionTitle({ children }: Readonly<{ children: React.ReactNode }>) {
  return <h3 className="text-xs font-medium text-muted-foreground">{children}</h3>;
}

function DetailRow({
  icon,
  label,
  children
}: Readonly<{ icon: React.ReactNode; label: React.ReactNode; children: React.ReactNode }>) {
  return (
    <div className="grid grid-cols-[1.25rem_1fr] gap-3">
      <span className="mt-0.5 text-muted-foreground [&_svg]:size-4">{icon}</span>
      <div className="flex min-w-0 flex-col gap-1">
        <span className="text-xs text-muted-foreground">{label}</span>
        <span className="text-sm break-words">{children}</span>
      </div>
    </div>
  );
}

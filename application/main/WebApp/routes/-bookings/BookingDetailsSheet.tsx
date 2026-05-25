import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  preferencesToTimeFormatOptions,
  useUserPreferences
} from "@repo/infrastructure/userPreferences/UserPreferencesContext";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@repo/ui/components/Sheet";
import { CalendarClockIcon, ClockIcon, LinkIcon } from "lucide-react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { BookingActionsDropdown } from "./BookingActionsDropdown";
import { BookingAttendeesSection } from "./BookingAttendeesSection";
import { DetailRow, SectionTitle } from "./BookingDetailsSheetParts";
import { BookingHistorySection } from "./BookingHistorySection";
import { BookingInternalNotesSection } from "./BookingInternalNotesSection";
import { type BookingListItem, formatBookingDateRange, getStatusVariant } from "./bookingTypes";

export function BookingDetailsSheet({
  booking,
  isOpen,
  onOpenChange
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}>) {
  return (
    <Sheet open={isOpen} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="sm:max-w-md">
        {booking && <BookingDetailsSheetBody booking={booking} onClose={() => onOpenChange(false)} />}
      </SheetContent>
    </Sheet>
  );
}

function BookingDetailsSheetBody({ booking, onClose }: Readonly<{ booking: BookingListItem; onClose: () => void }>) {
  const { hour12 } = preferencesToTimeFormatOptions(useUserPreferences());
  const { data: details, isPending } = api.useQuery("get", "/api/bookings/{id}", {
    params: { path: { id: booking.id } }
  });

  return (
    <>
      <SheetHeader className="border-b pr-12">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <SheetTitle>{booking.eventTypeTitle}</SheetTitle>
            <SheetDescription>{formatBookingDateRange(booking, hour12)}</SheetDescription>
          </div>
          <BookingActionsDropdown booking={booking} onActionComplete={onClose} />
        </div>
      </SheetHeader>
      <div className="flex flex-col gap-5 overflow-y-auto px-4 pb-4">
        <section className="rounded-md border p-4">
          <div className="mb-4 flex flex-wrap gap-2">
            <Badge variant={getStatusVariant(booking.status)}>{booking.status}</Badge>
            {booking.isRecurring && (
              <Badge variant="outline">
                <Trans>Recurring</Trans>
              </Badge>
            )}
            {details?.noShowHost === true && (
              <Badge variant="destructive">
                <Trans>Host no-show</Trans>
              </Badge>
            )}
          </div>
          <div className="grid gap-4">
            <DetailRow icon={<CalendarClockIcon />} label={<Trans>When</Trans>}>
              {formatBookingDateRange(booking, hour12)}
            </DetailRow>
            <DetailRow icon={<ClockIcon />} label={<Trans>Timezone</Trans>}>
              {booking.timeZone}
            </DetailRow>
            {(booking.locationValue || booking.locations.length > 0) && (
              <DetailRow icon={<LinkIcon />} label={<Trans>Location</Trans>}>
                <span className="inline-flex items-center gap-2">
                  <span className="min-w-0 break-all">
                    {booking.locationValue ??
                      booking.locations.map((location) => location.value ?? location.type).join(", ")}
                  </span>
                  {booking.locationValue?.startsWith("http") && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        void navigator.clipboard.writeText(booking.locationValue ?? "");
                        toast.success(t`Booking link copied`);
                      }}
                    >
                      <Trans>Copy</Trans>
                    </Button>
                  )}
                </span>
              </DetailRow>
            )}
          </div>
        </section>

        <BookingAttendeesSection
          isLoading={isPending}
          attendees={details?.attendees}
          fallbackName={booking.bookerName}
          fallbackEmail={booking.bookerEmail}
        />

        <BookingInternalNotesSection
          bookingId={booking.id}
          isLoading={isPending}
          notes={details?.internalNotes ?? []}
        />

        <BookingHistorySection isLoading={isPending} entries={details?.history ?? []} />

        {(booking.description || Object.entries(booking.responses).length > 0) && (
          <section className="rounded-md border p-4">
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
              <div className="rounded-md border">
                {Object.entries(booking.responses).map(([name, value]) => (
                  <div key={name} className="grid gap-1 border-b p-3 last:border-b-0">
                    <span className="text-xs text-muted-foreground">{name}</span>
                    <span className="text-sm">{value}</span>
                  </div>
                ))}
              </div>
            )}
          </section>
        )}
        <section className="rounded-md border p-4">
          <SectionTitle>
            <Trans>System</Trans>
          </SectionTitle>
          <span className="text-xs text-muted-foreground">
            <Trans>Booking ID</Trans>
          </span>
          <span className="mt-1 block text-sm break-all text-muted-foreground">{booking.id}</span>
        </section>
      </div>
    </>
  );
}

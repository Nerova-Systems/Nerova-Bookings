import { Trans } from "@lingui/react/macro";
import {
  preferencesToTimeFormatOptions,
  useUserPreferences
} from "@repo/infrastructure/userPreferences/UserPreferencesContext";
import { Badge } from "@repo/ui/components/Badge";
import { Checkbox } from "@repo/ui/components/Checkbox";
import { TableCell, TableRow } from "@repo/ui/components/Table";
import { cn } from "@repo/ui/utils";
import { LinkIcon, MapPinIcon, PhoneIcon, StarIcon, VideoIcon } from "lucide-react";

import { BookingActionsDropdown } from "./BookingActionsDropdown";
import { type BookingListItem, formatBookingDuration, getStatusVariant } from "./bookingTypes";

export function BookingsTableRow({
  booking,
  isSelected
}: Readonly<{
  booking: BookingListItem;
  isSelected: boolean;
}>) {
  const preferences = useUserPreferences();
  const { hour12 } = preferencesToTimeFormatOptions(preferences);
  const startTime = new Date(booking.startTime);
  const isCancelled = ["cancelled", "rejected"].includes(booking.status.toLowerCase());

  return (
    <TableRow rowKey={booking.id} data-testid="booking-item">
      <TableCell>
        <Checkbox checked={isSelected} aria-label="Select booking" />
      </TableCell>
      <TableCell>
        <Badge variant={getStatusVariant(booking.status)}>{booking.status}</Badge>
      </TableCell>
      <TableCell className="max-w-[16rem]">
        <div className="flex min-w-0 flex-col">
          <span className={cn("truncate text-sm font-medium", isCancelled && "text-muted-foreground line-through")}>
            {booking.eventTypeTitle}
          </span>
          {booking.isRecurring && (
            <Badge variant="outline" className="mt-1 w-fit">
              <Trans>Recurring</Trans>
            </Badge>
          )}
        </div>
      </TableCell>
      <TableCell className="max-w-[16rem]">
        <div className="flex min-w-0 flex-col">
          <span className="truncate text-sm">{booking.bookerName}</span>
          <span className="truncate text-xs text-muted-foreground">{booking.bookerEmail}</span>
        </div>
      </TableCell>
      <TableCell className="text-sm">
        <div className="flex flex-col">
          <span>{formatStartDate(startTime, booking.timeZone, hour12)}</span>
          <span className="text-xs text-muted-foreground">{booking.timeZone}</span>
        </div>
      </TableCell>
      <TableCell className="text-sm text-muted-foreground">{formatBookingDuration(booking)}</TableCell>
      <TableCell className="max-w-[12rem]">
        <BookingLocationCell booking={booking} />
      </TableCell>
      <TableCell>
        <BookingRatingCell rating={getBookingRating(booking)} />
      </TableCell>
      <TableCell className="text-right">
        <BookingActionsDropdown booking={booking} />
      </TableCell>
    </TableRow>
  );
}

function BookingLocationCell({ booking }: Readonly<{ booking: BookingListItem }>) {
  const value = booking.locationValue ?? booking.locations[0]?.value ?? booking.locationType ?? "";
  if (value.length === 0) {
    return <span className="text-sm text-muted-foreground">—</span>;
  }
  const Icon = getLocationIcon(booking.locationType ?? value);
  return (
    <span className="inline-flex min-w-0 items-center gap-1 truncate text-sm">
      <Icon className="size-4 shrink-0 text-muted-foreground" />
      <span className="truncate">{value}</span>
    </span>
  );
}

function getLocationIcon(value: string) {
  const lower = value.toLowerCase();
  if (lower.includes("zoom") || lower.includes("meet") || lower.includes("teams") || lower.includes("video")) {
    return VideoIcon;
  }
  if (lower.includes("phone") || lower.includes("call")) {
    return PhoneIcon;
  }
  if (lower.startsWith("http")) {
    return LinkIcon;
  }
  return MapPinIcon;
}

function BookingRatingCell({ rating }: Readonly<{ rating: number | null }>) {
  if (rating == null || rating <= 0) {
    return <span className="text-sm text-muted-foreground">—</span>;
  }
  return (
    <span className="inline-flex items-center gap-0.5 text-sm">
      {Array.from({ length: 5 }).map((_, index) => (
        <StarIcon
          // biome-ignore lint/suspicious/noArrayIndexKey: stars are a fixed-length array
          key={index}
          className={cn("size-3.5", index < rating ? "fill-amber-400 text-amber-400" : "text-muted-foreground/40")}
        />
      ))}
    </span>
  );
}

// Rating is exposed on `BookingDetailsResponse` only. The list endpoint does not include it today,
// so this returns null for list rows. When `BookingListItemResponse` is extended with a rating field
// (Wave 4.8) the cell will light up automatically.
function getBookingRating(_booking: BookingListItem): number | null {
  return null;
}

function formatStartDate(date: Date, timeZone: string, hour12?: boolean) {
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZone,
    ...(hour12 === undefined ? {} : { hour12 })
  }).format(date);
}

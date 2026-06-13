import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  preferencesToTimeFormatOptions,
  useUserPreferences
} from "@repo/infrastructure/userPreferences/UserPreferencesContext";
import { Badge } from "@repo/ui/components/Badge";
import { Checkbox } from "@repo/ui/components/Checkbox";
import { TableCell, TableRow } from "@repo/ui/components/Table";
import { cn } from "@repo/ui/utils";

import { BookingActionsDropdown } from "./BookingActionsDropdown";
import { type BookingListItem, formatBookingDateRange, getBookingStatusWords, getStatusVariant } from "./bookingTypes";

export function BookingsTableRow({
  booking,
  isSelected
}: Readonly<{
  booking: BookingListItem;
  isSelected: boolean;
}>) {
  const preferences = useUserPreferences();
  const { hour12 } = preferencesToTimeFormatOptions(preferences);
  const isCancelled = ["cancelled", "rejected"].includes(booking.status.toLowerCase());

  return (
    <TableRow rowKey={booking.id} data-testid="booking-item">
      <TableCell>
        <Checkbox checked={isSelected} aria-label={t`Select booking`} />
      </TableCell>
      <TableCell className="max-w-[16rem]">
        <div className="flex min-w-0 flex-col">
          <span className="truncate text-sm font-medium">{booking.bookerName}</span>
          <span className="truncate text-xs text-muted-foreground">{booking.bookerEmail}</span>
        </div>
      </TableCell>
      <TableCell className="max-w-[16rem]">
        <div className="flex min-w-0 flex-col">
          <span className={cn("truncate text-sm font-medium", isCancelled && "text-muted-foreground line-through")}>
            {booking.eventTypeTitle}
          </span>
          {booking.isRecurring && (
            <Badge variant="outline" className="mt-1 w-fit">
              <Trans>Repeats</Trans>
            </Badge>
          )}
        </div>
      </TableCell>
      <TableCell className="text-sm">
        <div className="flex flex-col">
          <span>{formatBookingDateRange(booking, hour12)}</span>
          <span className="text-xs text-muted-foreground">{booking.timeZone}</span>
        </div>
      </TableCell>
      <TableCell>
        <Badge variant={getStatusVariant(booking.status)}>{getBookingStatusWords(booking.status)}</Badge>
      </TableCell>
      <TableCell className="text-right">
        <BookingActionsDropdown booking={booking} />
      </TableCell>
    </TableRow>
  );
}

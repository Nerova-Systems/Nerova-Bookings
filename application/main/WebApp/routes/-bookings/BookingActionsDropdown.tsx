import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { DropdownMenu, DropdownMenuTrigger } from "@repo/ui/components/DropdownMenu";
import { EllipsisIcon } from "lucide-react";
import { useState } from "react";

import type { BookingDialogKind } from "./BookingActionDialogs";
import type { BookingListItem } from "./bookingTypes";

import { BookingActionDialogs } from "./BookingActionDialogs";
import { BookingActionsMenuContent } from "./BookingActionsMenuContent";

export function BookingActionsDropdown({
  booking,
  onActionComplete
}: Readonly<{
  booking: BookingListItem;
  onActionComplete?: () => void;
}>) {
  const [activeDialog, setActiveDialog] = useState<BookingDialogKind>(null);

  return (
    <>
      <DropdownMenu trackingTitle={t`Booking actions`}>
        <DropdownMenuTrigger
          render={
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              data-testid="booking-actions-dropdown"
              onClick={(event) => event.stopPropagation()}
            >
              <EllipsisIcon />
              <span className="sr-only">
                <Trans>Booking actions</Trans>
              </span>
            </Button>
          }
        />
        <BookingActionsMenuContent booking={booking} onSelectDialog={setActiveDialog} />
      </DropdownMenu>
      <BookingActionDialogs
        booking={booking}
        active={activeDialog}
        onClose={() => setActiveDialog(null)}
        onActionComplete={onActionComplete}
      />
    </>
  );
}

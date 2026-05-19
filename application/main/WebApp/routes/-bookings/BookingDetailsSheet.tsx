import { Sheet, SheetContent } from "@repo/ui/components/Sheet";

import { BookingDetailsBody, BookingDetailsFooter, BookingDetailsHeader } from "./BookingDetailsSheetContent";
import { type BookingListItem } from "./bookingTypes";

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
      <SheetContent side="right" className="gap-0 overflow-hidden pb-0 sm:max-w-[30rem] md:max-w-[34rem]">
        {booking && (
          <>
            <BookingDetailsHeader booking={booking} />
            <BookingDetailsBody booking={booking} />
            <BookingDetailsFooter booking={booking} onActionComplete={() => onOpenChange(false)} />
          </>
        )}
      </SheetContent>
    </Sheet>
  );
}

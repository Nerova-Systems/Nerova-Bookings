import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

import type { PublicBookingService } from "@/shared/lib/publicBookingApi";

import { money } from "@/shared/lib/appointmentsApi";

export function BookingFooter({
  selectedService,
  disabled,
  isSubmitting,
  onSubmit
}: {
  selectedService?: PublicBookingService;
  disabled: boolean;
  isSubmitting: boolean;
  onSubmit: () => void;
}) {
  return (
    <div className="sticky bottom-0 -mx-6 flex items-center justify-between gap-4 border-t border-border bg-background/95 px-6 py-4 backdrop-blur sm:-mx-8 sm:px-8 lg:-mx-10 lg:px-10">
      <div className="text-sm text-muted-foreground max-sm:hidden">{paymentText(selectedService)}</div>
      <Button onClick={onSubmit} disabled={disabled}>
        {isSubmitting ? <Trans>Confirming...</Trans> : <Trans>Confirm booking</Trans>}
      </Button>
    </div>
  );
}

function paymentText(selectedService?: PublicBookingService) {
  if (selectedService?.paymentPolicy === "DepositBeforeBooking" && selectedService.depositCents > 0) {
    return <span>Deposit required: {money(selectedService.depositCents)} via Paystack.</span>;
  }
  if (selectedService?.paymentPolicy === "FullPaymentBeforeBooking") {
    return <span>Full payment required before the booking is confirmed.</span>;
  }
  if (selectedService?.paymentPolicy === "CollectAfterAppointment") {
    return <span>Payment will be collected by the business after the appointment.</span>;
  }
  return <Trans>No deposit required for this service.</Trans>;
}

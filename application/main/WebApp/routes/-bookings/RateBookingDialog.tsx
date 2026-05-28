import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { Label } from "@repo/ui/components/Label";
import { Textarea } from "@repo/ui/components/Textarea";
import { StarIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";

export function RateBookingDialog({
  booking,
  isOpen,
  onOpenChange,
  onRated
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onRated?: () => void;
}>) {
  return (
    <Dialog trackingTitle={t`Rate booking`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Rate this booking</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Share how this meeting went. Your feedback is private to your team.</Trans>
          </DialogDescription>
        </DialogHeader>
        {booking && <RateBookingDialogBody booking={booking} onClose={() => onOpenChange(false)} onRated={onRated} />}
      </DialogContent>
    </Dialog>
  );
}

function RateBookingDialogBody({
  booking,
  onClose,
  onRated
}: Readonly<{ booking: BookingListItem; onClose: () => void; onRated?: () => void }>) {
  const [rating, setRating] = useState(0);
  const [feedback, setFeedback] = useState("");
  const mutation = api.useMutation("post", "/api/bookings/{id}/rate", {
    onSuccess: () => {
      toast.success(t`Rating saved`);
      void queryClient.invalidateQueries();
      onClose();
      onRated?.();
    }
  });

  return (
    <DialogForm
      validationErrors={mutation.error?.errors}
      onSubmit={() => {
        if (rating < 1) return;
        mutation.mutate({
          params: { path: { id: booking.id } },
          body: { id: booking.id, rating, feedback: feedback.trim() || null }
        });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={mutation.error} />
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label>
              <Trans>Rating</Trans>
            </Label>
            <div className="flex items-center gap-1">
              {[1, 2, 3, 4, 5].map((value) => (
                <Button
                  key={value}
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  aria-label={t`Rate ${value} of 5`}
                  onClick={() => setRating(value)}
                >
                  <StarIcon className={value <= rating ? "fill-primary text-primary" : "text-muted-foreground"} />
                </Button>
              ))}
            </div>
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="rate-feedback">
              <Trans>Feedback (optional)</Trans>
            </Label>
            <Textarea
              id="rate-feedback"
              name="feedback"
              value={feedback}
              placeholder={t`What went well, what could improve?`}
              onChange={(event) => setFeedback(event.currentTarget.value)}
            />
          </div>
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={mutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={mutation.isPending} disabled={rating < 1}>
          <Trans>Save rating</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

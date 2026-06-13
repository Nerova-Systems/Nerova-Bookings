import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Sheet, SheetContent, SheetFooter, SheetHeader, SheetTitle } from "@repo/ui/components/Sheet";
import { Textarea } from "@repo/ui/components/Textarea";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { downloadBookingsCsv } from "./exportBookingsCsv";

type BulkDialog = "cancel" | "no-show" | null;

export function BookingBulkActionBar({
  selectedBookings,
  onClear
}: Readonly<{
  selectedBookings: BookingListItem[];
  onClear: () => void;
}>) {
  const [dialog, setDialog] = useState<BulkDialog>(null);
  const count = selectedBookings.length;

  if (count === 0) {
    return null;
  }

  return (
    <>
      <div className="sticky bottom-4 z-20 mt-4 flex flex-wrap items-center justify-between gap-3 rounded-md border bg-card p-3 shadow-md">
        <span className="text-sm font-medium">
          <Trans>{count} selected</Trans>
        </span>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={() => setDialog("no-show")}>
            <Trans>Mark as no-show</Trans>
          </Button>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => {
              const filename = `bookings-selected-${new Date().toISOString().slice(0, 10)}.csv`;
              downloadBookingsCsv(selectedBookings, filename);
              toast.success(t`Exported ${count} bookings`);
            }}
          >
            <Trans>Export CSV</Trans>
          </Button>
          <Button type="button" variant="destructive" size="sm" onClick={() => setDialog("cancel")}>
            <Trans>Cancel selected</Trans>
          </Button>
          <Button type="button" variant="ghost" size="sm" onClick={onClear}>
            <Trans>Clear</Trans>
          </Button>
        </div>
      </div>
      <BookingBulkActionSheet
        kind={dialog}
        bookings={selectedBookings}
        onClose={() => setDialog(null)}
        onCompleted={() => {
          setDialog(null);
          onClear();
        }}
      />
    </>
  );
}

function BookingBulkActionSheet({
  kind,
  bookings,
  onClose,
  onCompleted
}: Readonly<{
  kind: BulkDialog;
  bookings: BookingListItem[];
  onClose: () => void;
  onCompleted: () => void;
}>) {
  const [reason, setReason] = useState("");
  const cancelMutation = api.useMutation("post", "/api/bookings/{id}/cancel");
  const noShowMutation = api.useMutation("post", "/api/bookings/{id}/no-show");
  const isPending = cancelMutation.isPending || noShowMutation.isPending;
  const isOpen = kind !== null;

  const onSubmit = async () => {
    const trimmed = reason.trim();
    try {
      if (kind === "cancel") {
        await Promise.all(
          bookings.map((booking) =>
            cancelMutation.mutateAsync({
              params: { path: { id: booking.id }, query: { reason: trimmed || null } }
            })
          )
        );
        toast.success(t`Cancelled ${bookings.length} bookings`);
      } else if (kind === "no-show") {
        await Promise.all(
          bookings.map((booking) =>
            noShowMutation.mutateAsync({
              params: { path: { id: booking.id } },
              body: { id: booking.id, attendeeId: null, noShow: true }
            })
          )
        );
        toast.success(t`Marked ${bookings.length} bookings as no-show`);
      }
      await queryClient.invalidateQueries();
      setReason("");
      onCompleted();
    } catch {
      // Errors are surfaced via the GeneralApiErrors block below.
    }
  };

  return (
    <Sheet open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <SheetContent side="right" className="sm:max-w-md">
        <SheetHeader>
          <SheetTitle>
            {kind === "cancel" ? (
              <Trans>Cancel {bookings.length} bookings?</Trans>
            ) : (
              <Trans>Mark {bookings.length} bookings as no-show?</Trans>
            )}
          </SheetTitle>
        </SheetHeader>
        <div className="flex flex-col gap-3 px-4">
          <GeneralApiErrors error={cancelMutation.error ?? noShowMutation.error} />
          {kind === "cancel" && (
            <Textarea
              autoFocus={true}
              value={reason}
              placeholder={t`Let clients know why these bookings are being cancelled`}
              onChange={(event) => setReason(event.currentTarget.value)}
            />
          )}
          <p className="text-sm text-muted-foreground">
            <Trans>This action applies to every selected booking.</Trans>
          </p>
        </div>
        <SheetFooter>
          <Button type="button" variant="secondary" disabled={isPending} onClick={onClose}>
            <Trans>Keep bookings</Trans>
          </Button>
          <Button
            type="button"
            variant={kind === "cancel" ? "destructive" : "default"}
            isPending={isPending}
            onClick={onSubmit}
          >
            <Trans>Apply to all</Trans>
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

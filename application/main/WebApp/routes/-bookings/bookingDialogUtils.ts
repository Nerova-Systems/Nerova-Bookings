import { toast } from "sonner";

import { queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

export type BookingDialogProps = Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onCompleted?: () => void;
}>;

export function completeAction(message: string, onOpenChange: (isOpen: boolean) => void, onCompleted?: () => void) {
  toast.success(message);
  void queryClient.invalidateQueries();
  onOpenChange(false);
  onCompleted?.();
}

export function nullableString(value: FormDataEntryValue | null) {
  const stringValue = String(value ?? "").trim();
  return stringValue.length > 0 ? stringValue : null;
}

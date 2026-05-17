import { isMediumViewportOrLarger } from "@repo/ui/utils/responsive";
import { useEffect } from "react";

import type { BookingDashboardView } from "./bookingTypes";

const storageKey = "bookings-preferred-view";

export function useBookingsView({
  view,
  onViewChange
}: Readonly<{
  view: BookingDashboardView;
  onViewChange: (view: BookingDashboardView) => void;
}>) {
  useEffect(() => {
    if (view === "calendar" && !isMediumViewportOrLarger()) {
      onViewChange("list");
    }
  }, [onViewChange, view]);

  useEffect(() => {
    if (view === "calendar" && !isMediumViewportOrLarger()) {
      return;
    }

    localStorage.setItem(storageKey, view);
  }, [view]);

  return [view, onViewChange] as const;
}

export function getStoredBookingsView(): BookingDashboardView {
  if (typeof window === "undefined") return "list";

  return localStorage.getItem(storageKey) === "calendar" && isMediumViewportOrLarger() ? "calendar" : "list";
}

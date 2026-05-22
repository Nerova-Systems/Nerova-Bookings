import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";

import { getWeekStartDate } from "../-bookings/bookingTypes";
import { formatWeekStartSearchValue } from "../-bookings/WeekPicker";

export const Route = createFileRoute("/bookings/")({
  staticData: { trackingTitle: "Bookings" },
  component: BookingsIndexRedirect
});

function BookingsIndexRedirect() {
  const navigate = useNavigate();

  useEffect(() => {
    navigate({
      to: "/bookings/$status",
      params: { status: "upcoming" },
      search: {
        search: undefined,
        eventTypeId: undefined,
        attendeeName: undefined,
        attendeeEmail: undefined,
        bookingUid: undefined,
        dateFrom: undefined,
        dateTo: undefined,
        noShowOnly: undefined,
        hasInternalNote: undefined,
        minRating: undefined,
        view: "list",
        weekStart: formatWeekStartSearchValue(getWeekStartDate(new Date())),
        pageOffset: 0
      },
      replace: true
    });
  }, [navigate]);

  return null;
}

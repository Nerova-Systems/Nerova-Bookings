import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";

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
        pageOffset: 0
      },
      replace: true
    });
  }, [navigate]);

  return null;
}

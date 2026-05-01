import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/dashboard/calendar/")({
  beforeLoad: () => {
    throw redirect({ to: "/dashboard/bookings", search: { view: "calendar" } });
  }
});

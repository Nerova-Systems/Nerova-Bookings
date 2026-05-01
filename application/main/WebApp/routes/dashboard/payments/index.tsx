import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/dashboard/payments/")({
  beforeLoad: () => {
    throw redirect({ to: "/dashboard/apps/installed", search: { category: "Payment" } });
  }
});

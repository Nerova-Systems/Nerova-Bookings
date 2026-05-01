import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/dashboard/apps/")({
  beforeLoad: () => {
    throw redirect({ to: "/dashboard/apps/store" });
  }
});

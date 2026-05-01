import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/dashboard/settings/out-of-office/")({
  beforeLoad: () => {
    throw redirect({ href: "/user/out-of-office" });
  }
});

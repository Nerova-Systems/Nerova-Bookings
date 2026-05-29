import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/whatsapp/$")({
  beforeLoad: () => {
    throw redirect({
      to: "/apps/installed",
      search: {
        open: "whatsapp"
      }
    });
  }
});

import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/channels/")({
  beforeLoad: () => {
    // /channels has no page of its own -- /channels/overview is the canonical landing surface.
    throw redirect({ to: "/channels/overview" });
  }
});

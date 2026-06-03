import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/channels/")({
  beforeLoad: () => {
    // Channels has a single surface today; WhatsApp is the canonical landing route.
    throw redirect({ to: "/channels/whatsapp" });
  }
});

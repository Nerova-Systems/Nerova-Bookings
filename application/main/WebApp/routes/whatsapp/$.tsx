import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/whatsapp/$")({
  beforeLoad: () => {
    // WhatsApp settings now live under the Channels section. Repoint the legacy deep link to the
    // single canonical home.
    throw redirect({ to: "/channels/whatsapp" });
  }
});

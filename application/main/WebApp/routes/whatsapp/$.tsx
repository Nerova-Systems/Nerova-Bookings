import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/whatsapp/$")({
  beforeLoad: () => {
    // WhatsApp settings now live under the Channels section, hosted by the account app via the
    // /channels bridge. Repoint the legacy deep link to the single canonical home.
    throw redirect({ to: "/channels/$", params: { _splat: "whatsapp" } });
  }
});

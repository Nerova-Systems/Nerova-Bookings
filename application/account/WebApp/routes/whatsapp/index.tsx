import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/whatsapp/")({
  beforeLoad: () => {
    // WhatsApp now lives under the Channels section. Keep this legacy route as a redirect so
    // /whatsapp resolves to the single canonical home at /channels/whatsapp.
    throw redirect({ to: "/channels/whatsapp" });
  }
});

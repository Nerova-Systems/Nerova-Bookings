import { requireAuthentication } from "@repo/infrastructure/auth/routeGuards";
import { createFileRoute, Outlet } from "@tanstack/react-router";

export const Route = createFileRoute("/whatsapp")({
  beforeLoad: () => requireAuthentication(),
  component: () => <Outlet />
});

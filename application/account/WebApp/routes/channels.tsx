import { requireAuthentication } from "@repo/infrastructure/auth/routeGuards";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute, Outlet } from "@tanstack/react-router";

import { AccountSideMenu } from "@/shared/components/AccountSideMenu";

export const Route = createFileRoute("/channels")({
  beforeLoad: () => requireAuthentication(),
  component: ChannelsLayout
});

function ChannelsLayout() {
  return (
    <SidebarProvider>
      <AccountSideMenu />
      <SidebarInset>
        <Outlet />
      </SidebarInset>
    </SidebarProvider>
  );
}

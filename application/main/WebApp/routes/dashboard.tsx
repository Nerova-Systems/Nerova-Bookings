import { requireAuthentication } from "@repo/infrastructure/auth/routeGuards";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute, Outlet, useLocation } from "@tanstack/react-router";
import { lazy, Suspense } from "react";

import { MainSideMenu } from "@/shared/components/MainSideMenu";

import { CmdkProvider, useCmdk } from "./dashboard/-components/CmdkContext";
import { CommandPalette } from "./dashboard/-components/CommandPalette";

const NotFoundPage = lazy(() => import("account/NotFoundPage"));
const TenantStateGuard = lazy(() => import("account/TenantStateGuard"));

export const Route = createFileRoute("/dashboard")({
  beforeLoad: () => requireAuthentication(),
  component: DashboardLayout,
  notFoundComponent: NotFoundPage
});

function DashboardLayout() {
  const location = useLocation();

  return (
    <Suspense fallback={null}>
      <TenantStateGuard pathname={location.pathname}>
        <CmdkProvider>
          <SidebarProvider>
            <MainSideMenu />
            <SidebarInset>
              <Outlet />
            </SidebarInset>
            <DashboardCommandPalette />
          </SidebarProvider>
        </CmdkProvider>
      </TenantStateGuard>
    </Suspense>
  );
}

function DashboardCommandPalette() {
  const { open, setOpen } = useCmdk();
  return <CommandPalette open={open} onOpenChange={setOpen} />;
}

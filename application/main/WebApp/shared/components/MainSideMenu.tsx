import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  collapsedContext,
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail
} from "@repo/ui/components/Sidebar";
import { Link as RouterLink, useNavigate, useRouter } from "@tanstack/react-router";
import MobileMenu from "account/MobileMenu";
import UserMenu from "account/UserMenu";
import { ActivityIcon, BarChart2Icon, CalendarIcon, Grid2X2Icon, UsersIcon } from "lucide-react";
import { use } from "react";

const normalizePath = (path: string): string => path.replace(/\/$/, "") || "/";

function HeaderUserMenu() {
  const isCollapsed = use(collapsedContext);
  return <UserMenu isCollapsed={isCollapsed} />;
}

export function MainSideMenu() {
  const router = useRouter();
  const currentPath = normalizePath(router.state.location.pathname);
  const navigate = useNavigate();
  const handleNavigate = (path: string) => {
    navigate({ to: path });
  };

  const isActive = (path: string) => currentPath === path || currentPath.startsWith(path + "/");

  return (
    <Sidebar collapsible="icon" mobileContent={<MobileMenu onNavigate={handleNavigate} />}>
      <nav className="contents" aria-label={t`Main navigation`}>
        <SidebarHeader>
          <HeaderUserMenu />
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>
              <Trans>Main</Trans>
            </SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={isActive("/dashboard") && currentPath === "/dashboard"}
                    tooltip={t`Activity`}
                  >
                    <RouterLink to="/dashboard">
                      <ActivityIcon />
                      <span>
                        <Trans>Activity</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/dashboard/calendar")} tooltip={t`Calendar`}>
                    <RouterLink to="/dashboard/calendar">
                      <CalendarIcon />
                      <span>
                        <Trans>Calendar</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/dashboard/services")} tooltip={t`Services`}>
                    <RouterLink to="/dashboard/services">
                      <Grid2X2Icon />
                      <span>
                        <Trans>Services</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/dashboard/clients")} tooltip={t`Clients`}>
                    <RouterLink to="/dashboard/clients">
                      <UsersIcon />
                      <span>
                        <Trans>Clients</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/dashboard/analytics")} tooltip={t`Analytics`}>
                    <RouterLink to="/dashboard/analytics">
                      <BarChart2Icon />
                      <span>
                        <Trans>Analytics</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>
      </nav>
      <SidebarRail />
    </Sidebar>
  );
}

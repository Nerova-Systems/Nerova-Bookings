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
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  SidebarRail
} from "@repo/ui/components/Sidebar";
import { Link as RouterLink, useNavigate, useRouter } from "@tanstack/react-router";
import MobileMenu from "account/MobileMenu";
import UserMenu from "account/UserMenu";
import {
  ActivityIcon,
  BarChart2Icon,
  CalendarIcon,
  ChevronDownIcon,
  CreditCardIcon,
  Grid2X2Icon,
  PlugIcon,
  UsersIcon
} from "lucide-react";
import { use, useEffect, useState } from "react";

const normalizePath = (path: string): string => path.replace(/\/$/, "") || "/";

function HeaderUserMenu() {
  const isCollapsed = use(collapsedContext);
  return <UserMenu isCollapsed={isCollapsed} />;
}

export function MainSideMenu() {
  const router = useRouter();
  const currentPath = normalizePath(router.state.location.pathname);
  const navigate = useNavigate();
  const [appsExpanded, setAppsExpanded] = useState(currentPath.startsWith("/dashboard/apps"));
  const handleNavigate = (path: string) => {
    navigate({ to: path });
  };

  const isActive = (path: string) => currentPath === path || currentPath.startsWith(path + "/");

  useEffect(() => {
    if (currentPath.startsWith("/dashboard/apps")) setAppsExpanded(true);
  }, [currentPath]);

  return (
    <Sidebar collapsible="icon" mobileContent={<MobileMenu onNavigate={handleNavigate} />}>
      <nav className="contents" aria-label={t`Main navigation`}>
        <SidebarHeader>
          <HeaderUserMenu />
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>
              <Trans>Workspace</Trans>
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
                  <SidebarMenuButton asChild={true} isActive={isActive("/dashboard/clients")} tooltip={t`Clients`}>
                    <RouterLink to="/dashboard/clients">
                      <UsersIcon />
                      <span>
                        <Trans>Clients</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
          <SidebarGroup>
            <SidebarGroupLabel>
              <Trans>Business</Trans>
            </SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/dashboard/payments")} tooltip={t`Payments`}>
                    <RouterLink to="/dashboard/payments">
                      <CreditCardIcon />
                      <span>
                        <Trans>Payments</Trans>
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
                  <SidebarMenuButton asChild={true} isActive={isActive("/dashboard/analytics")} tooltip={t`Analytics`}>
                    <RouterLink to="/dashboard/analytics">
                      <BarChart2Icon />
                      <span>
                        <Trans>Analytics</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    type="button"
                    isActive={isActive("/dashboard/apps")}
                    tooltip={t`Apps`}
                    onClick={() => {
                      setAppsExpanded((expanded) => !expanded);
                      if (!isActive("/dashboard/apps")) navigate({ to: "/dashboard/apps/store" });
                    }}
                  >
                    <PlugIcon />
                    <span>
                      <Trans>Apps</Trans>
                    </span>
                    <ChevronDownIcon className={`ml-auto transition-transform ${appsExpanded ? "rotate-180" : ""}`} />
                  </SidebarMenuButton>
                  <SidebarMenuSub isExpanded={appsExpanded}>
                    <SidebarMenuSubItem>
                      <SidebarMenuSubButton asChild={true} isActive={isActive("/dashboard/apps/store")} tooltip={t`App store`}>
                        <RouterLink to="/dashboard/apps/store">
                          <Grid2X2Icon />
                          <span>
                            <Trans>App store</Trans>
                          </span>
                        </RouterLink>
                      </SidebarMenuSubButton>
                    </SidebarMenuSubItem>
                    <SidebarMenuSubItem>
                      <SidebarMenuSubButton asChild={true} isActive={isActive("/dashboard/apps/installed")} tooltip={t`Installed apps`}>
                        <RouterLink to="/dashboard/apps/installed">
                          <PlugIcon />
                          <span>
                            <Trans>Installed apps</Trans>
                          </span>
                        </RouterLink>
                      </SidebarMenuSubButton>
                    </SidebarMenuSubItem>
                  </SidebarMenuSub>
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

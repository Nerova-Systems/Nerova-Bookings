import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useFeatureFlag } from "@repo/infrastructure/featureFlags/useFeatureFlag";
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
import { BarChart3Icon, CalendarCheckIcon, CalendarDaysIcon, LayoutDashboardIcon, TimerIcon } from "lucide-react";
import { use } from "react";

import { getWeekStartDate } from "@/routes/-bookings/bookingTypes";
import { formatWeekStartSearchValue } from "@/routes/-bookings/WeekPicker";

const normalizePath = (path: string): string => path.replace(/\/$/, "") || "/";

function HeaderUserMenu() {
  // Federated UserMenu reads the same shimmed `collapsedContext` value provided by SidebarProvider.
  const isCollapsed = use(collapsedContext);
  return <UserMenu isCollapsed={isCollapsed} />;
}

export function MainSideMenu() {
  const router = useRouter();
  const currentPath = normalizePath(router.state.location.pathname);
  const navigate = useNavigate();
  const { enabled: isInsightsEnabled } = useFeatureFlag("cap-insights");
  const handleNavigate = (path: string) => {
    navigate({ to: path });
  };

  return (
    <Sidebar collapsible="icon" mobileContent={<MobileMenu onNavigate={handleNavigate} />}>
      <nav className="contents" aria-label={t`Main navigation`}>
        <SidebarHeader>
          <HeaderUserMenu />
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>
              <Trans>Navigation</Trans>
            </SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={currentPath === "/dashboard"} tooltip={t`Dashboard`}>
                    <RouterLink to="/dashboard">
                      <LayoutDashboardIcon />
                      <span>
                        <Trans>Dashboard</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={currentPath.startsWith("/event-types")}
                    tooltip={t`Event types`}
                  >
                    <RouterLink to="/event-types" search={{ dialog: undefined, duplicateEventTypeId: undefined }}>
                      <TimerIcon />
                      <span>
                        <Trans>Event types</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={currentPath.startsWith("/bookings")}
                    tooltip={t`Bookings`}
                  >
                    <RouterLink
                      to="/bookings/$status"
                      params={{ status: "upcoming" }}
                      search={{
                        search: undefined,
                        eventTypeId: undefined,
                        attendeeName: undefined,
                        attendeeEmail: undefined,
                        bookingUid: undefined,
                        dateFrom: undefined,
                        dateTo: undefined,
                        view: "list",
                        weekStart: formatWeekStartSearchValue(getWeekStartDate(new Date())),
                        pageOffset: 0
                      }}
                    >
                      <CalendarCheckIcon />
                      <span>
                        <Trans>Bookings</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={currentPath.startsWith("/availability")}
                    tooltip={t`Availability`}
                  >
                    <RouterLink to="/availability">
                      <CalendarDaysIcon />
                      <span>
                        <Trans>Availability</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                {isInsightsEnabled && (
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      asChild={true}
                      isActive={currentPath.startsWith("/insights")}
                      tooltip={t`Insights`}
                    >
                      <RouterLink to="/insights" search={{ from: undefined, to: undefined }}>
                        <BarChart3Icon />
                        <span>
                          <Trans>Insights</Trans>
                        </span>
                      </RouterLink>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                )}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>
      </nav>
      <SidebarRail />
    </Sidebar>
  );
}

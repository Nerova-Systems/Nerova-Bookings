import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
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
import { Link as RouterLink, useRouter } from "@tanstack/react-router";
import { BoxIcon, Building2Icon, InboxIcon, MailIcon, RadioTowerIcon, UsersIcon } from "lucide-react";

import logoMark from "@/shared/images/logo-mark.svg";

const normalizePath = (path: string): string => path.replace(/\/$/, "") || "/";

export function BackOfficeSideMenu() {
  const router = useRouter();
  const currentPath = normalizePath(router.state.location.pathname);

  return (
    <Sidebar collapsible="icon">
      <nav className="contents" aria-label={t`Main navigation`}>
        <SidebarHeader>
          <div className="flex items-center gap-3 pl-[0.875rem] text-sm font-semibold">
            <img className="size-9 shrink-0" src={logoMark} alt={t`PlatformPlatform logo`} />
            <span className="truncate group-data-[collapsible=icon]:hidden">PlatformPlatform</span>
          </div>
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>
              <Trans>Navigation</Trans>
            </SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={currentPath === "/back-office"} tooltip={t`Dashboard`}>
                    <RouterLink to="/back-office">
                      <BoxIcon />
                      <span>
                        <Trans>Dashboard</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={currentPath.startsWith("/back-office/tenants")}
                    tooltip={t`Tenants`}
                  >
                    <RouterLink to="/back-office/tenants">
                      <Building2Icon />
                      <span>
                        <Trans>Tenants</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={currentPath.startsWith("/back-office/users")}
                    tooltip={t`Users`}
                  >
                    <RouterLink to="/back-office/users">
                      <UsersIcon />
                      <span>
                        <Trans>Users</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={currentPath.startsWith("/back-office/outbox")}
                    tooltip={t`Outbox`}
                  >
                    <RouterLink to="/back-office/outbox">
                      <InboxIcon />
                      <span>
                        <Trans>Outbox</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={currentPath.startsWith("/back-office/email")} tooltip={t`Email`}>
                    <RouterLink to="/back-office/email">
                      <MailIcon />
                      <span>
                        <Trans>Email</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={currentPath.startsWith("/back-office/messaging-health")}
                    tooltip={t`Messaging`}
                  >
                    <RouterLink to="/back-office/messaging-health">
                      <RadioTowerIcon />
                      <span>
                        <Trans>Messaging</Trans>
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

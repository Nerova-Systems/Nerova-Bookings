import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
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
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail
} from "@repo/ui/components/Sidebar";
import { Link as RouterLink, useRouter } from "@tanstack/react-router";
import { LifeBuoyIcon } from "lucide-react";
import { use } from "react";

import MobileMenu from "@/federated-modules/sideMenu/MobileMenu";
import UserMenu from "@/federated-modules/userMenu/UserMenu";
import { useMainNavigation } from "@/shared/hooks/useMainNavigation";
import { api } from "@/shared/lib/api/client";

import { AccountGroup, UserGroup } from "./AccountSideMenu.parts";

const normalizePath = (path: string): string => path.replace(/\/$/, "") || "/";

function HeaderUserMenu() {
  // Federated UserMenu reads `collapsedContext` (shimmed by SidebarProvider in new Sidebar).
  const isCollapsed = use(collapsedContext);
  return <UserMenu isCollapsed={isCollapsed} />;
}

export function AccountSideMenu() {
  const userInfo = useUserInfo();
  const router = useRouter();
  const currentPath = normalizePath(router.state.location.pathname);
  const { navigateToMain } = useMainNavigation();
  const { enabled: isSubscriptionEnabled } = useFeatureFlag("subscriptions");
  const { enabled: isAccountOverviewEnabled } = useFeatureFlag("account-overview");
  const { enabled: isTierEnterpriseEnabled } = useFeatureFlag("tier-enterprise");
  const { enabled: isTierTeamsEnabled } = useFeatureFlag("tier-teams");
  const { enabled: isTierOrganizationsEnabled } = useFeatureFlag("tier-organizations");
  const { enabled: isAuditLogEnabled } = useFeatureFlag("cap-audit-log");
  const { enabled: isSupportSystemEnabled } = useFeatureFlag("support-system");
  // The /api/account/support-tickets endpoint is gone when the support system is disabled, so the
  // query must be conditionally enabled to avoid a guaranteed 404 on every page load.
  const { data: myTickets } = api.useQuery(
    "get",
    "/api/account/support-tickets",
    {},
    { enabled: isSupportSystemEnabled }
  );
  const awaitingUserCount = myTickets?.awaitingUserCount ?? 0;

  const isActive = (target: string, matchPrefix = false) => {
    const normalized = normalizePath(target);
    return matchPrefix ? currentPath.startsWith(normalized) : currentPath === normalized;
  };

  const isPrivileged = userInfo?.role === "Owner" || userInfo?.role === "Admin";
  const showBilling = userInfo?.role === "Owner" && isSubscriptionEnabled;
  const showRoles = isPrivileged && isTierEnterpriseEnabled;
  const showTeams = isPrivileged && isTierTeamsEnabled;
  const showAuditLog = isPrivileged && isAuditLogEnabled;

  return (
    <Sidebar collapsible="icon" mobileContent={<MobileMenu onNavigate={navigateToMain ?? undefined} />}>
      <nav className="contents" aria-label={t`Main navigation`}>
        <SidebarHeader>
          <HeaderUserMenu />
        </SidebarHeader>
        <SidebarContent>
          <UserGroup isActive={isActive} />
          <AccountGroup
            isActive={isActive}
            isAccountOverviewEnabled={isAccountOverviewEnabled}
            showTeams={showTeams}
            showRoles={showRoles}
            showAuditLog={showAuditLog}
            showBilling={showBilling}
          />
          {isSupportSystemEnabled && (
            <SupportSidebarGroup isActive={isActive("/support/tickets", true)} awaitingUserCount={awaitingUserCount} />
          )}
        </SidebarContent>
      </nav>
      <SidebarRail />
    </Sidebar>
  );
}

function SupportSidebarGroup({ isActive, awaitingUserCount }: { isActive: boolean; awaitingUserCount: number }) {
  return (
    <SidebarGroup>
      <SidebarGroupLabel>
        <Trans>Support</Trans>
      </SidebarGroupLabel>
      <SidebarGroupContent>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton asChild={true} isActive={isActive} tooltip={t`My tickets`}>
              <RouterLink to="/support/tickets">
                <LifeBuoyIcon />
                <span>
                  <Trans>My tickets</Trans>
                </span>
              </RouterLink>
            </SidebarMenuButton>
            {awaitingUserCount > 0 && <SidebarMenuBadge>{awaitingUserCount}</SidebarMenuBadge>}
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarGroupContent>
    </SidebarGroup>
  );
}

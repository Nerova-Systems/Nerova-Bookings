import { t } from "@lingui/core/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { useFeatureFlag } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { collapsedContext, Sidebar, SidebarContent, SidebarHeader, SidebarRail } from "@repo/ui/components/Sidebar";
import { useRouter } from "@tanstack/react-router";
import { use } from "react";

import MobileMenu from "@/federated-modules/sideMenu/MobileMenu";
import UserMenu from "@/federated-modules/userMenu/UserMenu";
import { useMainNavigation } from "@/shared/hooks/useMainNavigation";

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

  const isActive = (target: string, matchPrefix = false) => {
    const normalized = normalizePath(target);
    return matchPrefix ? currentPath.startsWith(normalized) : currentPath === normalized;
  };

  const isPrivileged = userInfo?.role === "Owner" || userInfo?.role === "Admin";
  const showBilling = userInfo?.role === "Owner" && isSubscriptionEnabled;
  const showRoles = isPrivileged && isTierEnterpriseEnabled;
  const showTeams = isPrivileged && isTierTeamsEnabled;
  const showOrganization = isPrivileged && isTierOrganizationsEnabled;

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
            showOrganization={showOrganization}
            showTeams={showTeams}
            showRoles={showRoles}
            showBilling={showBilling}
          />
        </SidebarContent>
      </nav>
      <SidebarRail />
    </Sidebar>
  );
}

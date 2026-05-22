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
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail
} from "@repo/ui/components/Sidebar";
import { Link as RouterLink, useRouter } from "@tanstack/react-router";
import {
  Building2Icon,
  CreditCardIcon,
  HomeIcon,
  MonitorSmartphoneIcon,
  ShieldCheckIcon,
  SlidersHorizontalIcon,
  UserIcon,
  UsersIcon,
  UsersRoundIcon
} from "lucide-react";
import { type ReactNode, use } from "react";

import MobileMenu from "@/federated-modules/sideMenu/MobileMenu";
import UserMenu from "@/federated-modules/userMenu/UserMenu";
import { useMainNavigation } from "@/shared/hooks/useMainNavigation";

const normalizePath = (path: string): string => path.replace(/\/$/, "") || "/";

function HeaderUserMenu() {
  // Federated UserMenu reads `collapsedContext` (shimmed by SidebarProvider in new Sidebar).
  const isCollapsed = use(collapsedContext);
  return <UserMenu isCollapsed={isCollapsed} />;
}

type AccountNavItemProps = {
  to: string;
  icon: ReactNode;
  label: ReactNode;
  tooltip: string;
  isActive: boolean;
};

function AccountNavItem({ to, icon, label, tooltip, isActive }: Readonly<AccountNavItemProps>) {
  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild={true} isActive={isActive} tooltip={tooltip}>
        <RouterLink to={to}>
          {icon}
          <span>{label}</span>
        </RouterLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
  );
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

  const isActive = (target: string, matchPrefix = false) => {
    const normalized = normalizePath(target);
    return matchPrefix ? currentPath.startsWith(normalized) : currentPath === normalized;
  };

  const showBilling = userInfo?.role === "Owner" && isSubscriptionEnabled;
  const showRoles = (userInfo?.role === "Owner" || userInfo?.role === "Admin") && isTierEnterpriseEnabled;
  const showTeams = (userInfo?.role === "Owner" || userInfo?.role === "Admin") && isTierTeamsEnabled;

  return (
    <Sidebar collapsible="icon" mobileContent={<MobileMenu onNavigate={navigateToMain ?? undefined} />}>
      <nav className="contents" aria-label={t`Main navigation`}>
        <SidebarHeader>
          <HeaderUserMenu />
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>
              <Trans>User</Trans>
            </SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/user/profile")} tooltip={t`User profile`}>
                    <RouterLink to="/user/profile" aria-label={t`User profile`}>
                      <UserIcon />
                      <span>
                        <Trans>Profile</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    asChild={true}
                    isActive={isActive("/user/preferences")}
                    tooltip={t`User preferences`}
                  >
                    <RouterLink to="/user/preferences" aria-label={t`User preferences`}>
                      <SlidersHorizontalIcon />
                      <span>
                        <Trans>Preferences</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/user/sessions")} tooltip={t`User sessions`}>
                    <RouterLink to="/user/sessions" aria-label={t`User sessions`}>
                      <MonitorSmartphoneIcon />
                      <span>
                        <Trans>Sessions</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>

          <SidebarGroup>
            <SidebarGroupLabel>
              <Trans>Account</Trans>
            </SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {isAccountOverviewEnabled && (
                  <SidebarMenuItem>
                    <SidebarMenuButton asChild={true} isActive={isActive("/account")} tooltip={t`Overview`}>
                      <RouterLink to="/account">
                        <HomeIcon />
                        <span>
                          <Trans>Overview</Trans>
                        </span>
                      </RouterLink>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                )}
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/account/settings")} tooltip={t`Settings`}>
                    <RouterLink to="/account/settings">
                      <Building2Icon />
                      <span>
                        <Trans>Settings</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton asChild={true} isActive={isActive("/account/users", true)} tooltip={t`Users`}>
                    <RouterLink to="/account/users">
                      <UsersIcon />
                      <span>
                        <Trans>Users</Trans>
                      </span>
                    </RouterLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                {showTeams && (
                  <AccountNavItem
                    to="/account/settings/teams"
                    icon={<UsersRoundIcon />}
                    label={<Trans>Teams</Trans>}
                    tooltip={t`Teams`}
                    isActive={isActive("/account/settings/teams", true)}
                  />
                )}
                {showRoles && (
                  <AccountNavItem
                    to="/account/settings/roles"
                    icon={<ShieldCheckIcon />}
                    label={<Trans>Roles</Trans>}
                    tooltip={t`Roles`}
                    isActive={isActive("/account/settings/roles", true)}
                  />
                )}
                {showBilling && (
                  <AccountNavItem
                    to="/account/billing"
                    icon={<CreditCardIcon />}
                    label={<Trans>Billing</Trans>}
                    tooltip={t`Billing`}
                    isActive={isActive("/account/billing", true)}
                  />
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

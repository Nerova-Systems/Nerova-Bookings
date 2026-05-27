/* eslint-disable max-lines */
import type { ReactNode } from "react";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem
} from "@repo/ui/components/Sidebar";
import { Link as RouterLink } from "@tanstack/react-router";
import {
  Building2Icon,
  ClockIcon,
  CreditCardIcon,
  FileClockIcon,
  HomeIcon,
  LandmarkIcon,
  MessageCircleIcon,
  MonitorSmartphoneIcon,
  ShieldCheckIcon,
  SlidersHorizontalIcon,
  UserIcon,
  UsersIcon,
  UsersRoundIcon
} from "lucide-react";

export type IsActiveFn = (target: string, matchPrefix?: boolean) => boolean;

type AccountNavItemProps = {
  to: string;
  icon: ReactNode;
  label: ReactNode;
  tooltip: string;
  isActive: boolean;
};

export function AccountNavItem({ to, icon, label, tooltip, isActive }: Readonly<AccountNavItemProps>) {
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

export function UserGroup({ isActive }: Readonly<{ isActive: IsActiveFn }>) {
  return (
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
            <SidebarMenuButton asChild={true} isActive={isActive("/user/general")} tooltip={t`General`}>
              <RouterLink to="/user/general" aria-label={t`General`}>
                <ClockIcon />
                <span>
                  <Trans>General</Trans>
                </span>
              </RouterLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
          <SidebarMenuItem>
            <SidebarMenuButton asChild={true} isActive={isActive("/user/preferences")} tooltip={t`User preferences`}>
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
  );
}

export type AccountGroupProps = {
  isActive: IsActiveFn;
  isAccountOverviewEnabled: boolean;
  showOrganization: boolean;
  showTeams: boolean;
  showRoles: boolean;
  showAuditLog: boolean;
  showBilling: boolean;
};

export function AccountGroup(props: Readonly<AccountGroupProps>) {
  const { isActive } = props;
  return (
    <SidebarGroup>
      <SidebarGroupLabel>
        <Trans>Account</Trans>
      </SidebarGroupLabel>
      <SidebarGroupContent>
        <SidebarMenu>
          {props.isAccountOverviewEnabled && (
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
          {props.showOrganization && (
            <AccountNavItem
              to="/account/settings/organization"
              icon={<LandmarkIcon />}
              label={<Trans>Organization</Trans>}
              tooltip={t`Organization`}
              isActive={isActive("/account/settings/organization", true)}
            />
          )}
          {props.showTeams && (
            <AccountNavItem
              to="/account/settings/teams"
              icon={<UsersRoundIcon />}
              label={<Trans>Teams</Trans>}
              tooltip={t`Teams`}
              isActive={isActive("/account/settings/teams", true)}
            />
          )}
          {props.showRoles && (
            <AccountNavItem
              to="/account/settings/roles"
              icon={<ShieldCheckIcon />}
              label={<Trans>Roles</Trans>}
              tooltip={t`Roles`}
              isActive={isActive("/account/settings/roles", true)}
            />
          )}
          {props.showAuditLog && (
            <AccountNavItem
              to="/account/settings/audit-log"
              icon={<FileClockIcon />}
              label={<Trans>Audit log</Trans>}
              tooltip={t`Audit log`}
              isActive={isActive("/account/settings/audit-log", true)}
            />
          )}
          {props.showBilling && (
            <AccountNavItem
              to="/account/billing"
              icon={<CreditCardIcon />}
              label={<Trans>Billing</Trans>}
              tooltip={t`Billing`}
              isActive={isActive("/account/billing", true)}
            />
          )}
          <AccountNavItem
            to="/whatsapp"
            icon={<MessageCircleIcon />}
            label={<Trans>WhatsApp</Trans>}
            tooltip={t`WhatsApp`}
            isActive={isActive("/whatsapp", true)}
          />
        </SidebarMenu>
      </SidebarGroupContent>
    </SidebarGroup>
  );
}

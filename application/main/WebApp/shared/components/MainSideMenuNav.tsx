import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  SidebarMenuAction,
  SidebarMenuButton,
  SidebarMenuFlyout,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  useSidebarMenuCollapsible
} from "@repo/ui/components/Sidebar";
import { Link as RouterLink } from "@tanstack/react-router";
import {
  ChevronRightIcon,
  LayoutGridIcon,
  LayoutListIcon,
  MessageCircleIcon,
  MessageSquareIcon,
  PackageCheckIcon,
  StoreIcon,
  UploadIcon,
  UsersIcon
} from "lucide-react";
import { useEffect } from "react";

const flyoutLinkClassName =
  "flex items-center gap-3 rounded-md px-2 py-1.5 text-sm text-muted-foreground outline-ring hover:bg-sidebar-accent hover:text-sidebar-accent-foreground focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 data-[active=true]:font-medium data-[active=true]:text-foreground [&>svg]:size-4 [&>svg]:shrink-0";

/**
 * Collapsible "Apps" group: a top-level button (navigates to the App Store + expands the group) with
 * an App store / Installed apps sub list. Mirrors cal.com's nested Apps navigation and reuses the
 * shared Sidebar collapsible primitives (flyout when the sidebar is icon-only, chevron toggle,
 * animated sub list).
 */
export function AppsNavSection({ currentPath }: Readonly<{ currentPath: string }>) {
  const { isExpanded, toggle, expand } = useSidebarMenuCollapsible("apps");
  const isOnApps = currentPath === "/apps" || currentPath.startsWith("/apps/");
  useEffect(() => {
    if (isOnApps) expand();
  }, [isOnApps, expand]);

  const flyout = (
    <div className="flex flex-col gap-1 p-1">
      <div className="px-2 pt-1 pb-0.5 text-xs font-medium text-muted-foreground uppercase">
        <Trans>Apps</Trans>
      </div>
      <RouterLink to="/apps" data-active={currentPath === "/apps"} className={flyoutLinkClassName}>
        <StoreIcon />
        <span>
          <Trans>App store</Trans>
        </span>
      </RouterLink>
      <RouterLink
        to="/apps/installed"
        data-active={currentPath.startsWith("/apps/installed")}
        className={flyoutLinkClassName}
      >
        <PackageCheckIcon />
        <span>
          <Trans>Installed apps</Trans>
        </span>
      </RouterLink>
    </div>
  );

  return (
    <SidebarMenuItem>
      <SidebarMenuFlyout content={flyout} disabled={isExpanded}>
        <SidebarMenuButton asChild={true} isActive={isOnApps} onClick={expand} tooltip={t`Apps`}>
          <RouterLink to="/apps">
            <LayoutGridIcon />
            <span>
              <Trans>Apps</Trans>
            </span>
          </RouterLink>
        </SidebarMenuButton>
      </SidebarMenuFlyout>
      <SidebarMenuAction onClick={toggle} aria-label={isExpanded ? t`Collapse Apps` : t`Expand Apps`}>
        <ChevronRightIcon className={`transition-transform duration-100 ${isExpanded ? "rotate-90" : ""}`} />
      </SidebarMenuAction>
      <SidebarMenuSub isExpanded={isExpanded}>
        <SidebarMenuSubItem>
          <SidebarMenuSubButton asChild={true} isActive={currentPath === "/apps"} tooltip={{ children: t`App store` }}>
            <RouterLink to="/apps">
              <StoreIcon />
              <span>
                <Trans>App store</Trans>
              </span>
            </RouterLink>
          </SidebarMenuSubButton>
        </SidebarMenuSubItem>
        <SidebarMenuSubItem>
          <SidebarMenuSubButton
            asChild={true}
            isActive={currentPath.startsWith("/apps/installed")}
            tooltip={{ children: t`Installed apps` }}
          >
            <RouterLink to="/apps/installed">
              <PackageCheckIcon />
              <span>
                <Trans>Installed apps</Trans>
              </span>
            </RouterLink>
          </SidebarMenuSubButton>
        </SidebarMenuSubItem>
      </SidebarMenuSub>
    </SidebarMenuItem>
  );
}

export function ClientsNavSection({ currentPath }: Readonly<{ currentPath: string }>) {
  return (
    <>
      <SidebarMenuItem>
        <SidebarMenuButton asChild={true} isActive={currentPath.startsWith("/clients")} tooltip={t`Clients`}>
          <RouterLink to="/clients">
            <UsersIcon />
            <span>
              <Trans>Clients</Trans>
            </span>
          </RouterLink>
        </SidebarMenuButton>
      </SidebarMenuItem>
      <SidebarMenuItem>
        <SidebarMenuButton asChild={true} isActive={currentPath.startsWith("/import")} tooltip={t`Import clients`}>
          <RouterLink to="/import">
            <UploadIcon />
            <span>
              <Trans>Import clients</Trans>
            </span>
          </RouterLink>
        </SidebarMenuButton>
      </SidebarMenuItem>
    </>
  );
}

/**
 * Collapsible "Channels" group: a top-level button (navigates to the Channels overview + expands the
 * group) with an Overview / WhatsApp sub list. WhatsApp Business lives here rather than in the generic
 * App Store.
 */
export function ChannelsNavSection({ currentPath }: Readonly<{ currentPath: string }>) {
  const { isExpanded, toggle, expand } = useSidebarMenuCollapsible("channels");
  const isOnChannels = currentPath === "/channels" || currentPath.startsWith("/channels/");
  useEffect(() => {
    if (isOnChannels) expand();
  }, [isOnChannels, expand]);

  const flyout = (
    <div className="flex flex-col gap-1 p-1">
      <div className="px-2 pt-1 pb-0.5 text-xs font-medium text-muted-foreground uppercase">
        <Trans>Channels</Trans>
      </div>
      <RouterLink to="/channels" data-active={currentPath === "/channels"} className={flyoutLinkClassName}>
        <LayoutListIcon />
        <span>
          <Trans>Overview</Trans>
        </span>
      </RouterLink>
      <RouterLink
        to="/channels/whatsapp"
        data-active={currentPath.startsWith("/channels/whatsapp")}
        className={flyoutLinkClassName}
      >
        <MessageSquareIcon />
        <span>
          <Trans>WhatsApp</Trans>
        </span>
      </RouterLink>
    </div>
  );

  return (
    <SidebarMenuItem>
      <SidebarMenuFlyout content={flyout} disabled={isExpanded}>
        <SidebarMenuButton asChild={true} isActive={isOnChannels} onClick={expand} tooltip={t`Channels`}>
          <RouterLink to="/channels">
            <MessageCircleIcon />
            <span>
              <Trans>Channels</Trans>
            </span>
          </RouterLink>
        </SidebarMenuButton>
      </SidebarMenuFlyout>
      <SidebarMenuAction onClick={toggle} aria-label={isExpanded ? t`Collapse Channels` : t`Expand Channels`}>
        <ChevronRightIcon className={`transition-transform duration-100 ${isExpanded ? "rotate-90" : ""}`} />
      </SidebarMenuAction>
      <SidebarMenuSub isExpanded={isExpanded}>
        <SidebarMenuSubItem>
          <SidebarMenuSubButton
            asChild={true}
            isActive={currentPath === "/channels"}
            tooltip={{ children: t`Overview` }}
          >
            <RouterLink to="/channels">
              <LayoutListIcon />
              <span>
                <Trans>Overview</Trans>
              </span>
            </RouterLink>
          </SidebarMenuSubButton>
        </SidebarMenuSubItem>
        <SidebarMenuSubItem>
          <SidebarMenuSubButton
            asChild={true}
            isActive={currentPath.startsWith("/channels/whatsapp")}
            tooltip={{ children: t`WhatsApp` }}
          >
            <RouterLink to="/channels/whatsapp">
              <MessageSquareIcon />
              <span>
                <Trans>WhatsApp</Trans>
              </span>
            </RouterLink>
          </SidebarMenuSubButton>
        </SidebarMenuSubItem>
      </SidebarMenuSub>
    </SidebarMenuItem>
  );
}

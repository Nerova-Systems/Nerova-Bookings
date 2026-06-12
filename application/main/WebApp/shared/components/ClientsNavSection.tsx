import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { SidebarMenuButton, SidebarMenuItem } from "@repo/ui/components/Sidebar";
import { Link as RouterLink } from "@tanstack/react-router";
import { UploadIcon, UsersIcon } from "lucide-react";

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

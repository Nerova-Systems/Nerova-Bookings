import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { trackInteraction } from "@repo/infrastructure/applicationInsights/ApplicationInsightsProvider";
import { Button } from "@repo/ui/components/Button";
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuSeparator,
  ContextMenuTrigger
} from "@repo/ui/components/ContextMenu";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from "@repo/ui/components/DropdownMenu";
import { EllipsisVerticalIcon, PencilIcon, Trash2Icon, UserIcon } from "lucide-react";

import type { components } from "@/shared/lib/api/client";

type ClientDetails = components["schemas"]["ClientDetails"];

interface ClientActionMenuProps {
  client: ClientDetails;
  onSelectedClientsChange: (clients: ClientDetails[]) => void;
  onViewProfile: (client: ClientDetails) => void;
  onManageClient: (client: ClientDetails) => void;
  onDeleteClient: (client: ClientDetails) => void;
}

interface MobileClientActionMenuProps extends ClientActionMenuProps {
  children: React.ReactNode;
}

export function MobileClientActionMenu({
  client,
  onSelectedClientsChange,
  onViewProfile,
  onManageClient,
  onDeleteClient,
  children
}: Readonly<MobileClientActionMenuProps>) {
  return (
    <ContextMenu
      onOpenChange={(isOpen) => {
        if (isOpen) {
          onSelectedClientsChange([client]);
          trackInteraction("Client actions", "menu", "Open");
        }
      }}
    >
      <ContextMenuTrigger className="block w-full">{children}</ContextMenuTrigger>
      <ContextMenuContent className="w-auto">
        <ContextMenuItem onClick={() => onViewProfile(client)}>
          <UserIcon className="size-4" />
          <Trans>View profile</Trans>
        </ContextMenuItem>
        <ContextMenuItem onClick={() => onManageClient(client)}>
          <PencilIcon className="size-4" />
          <Trans>Manage client</Trans>
        </ContextMenuItem>
        <ContextMenuSeparator />
        <ContextMenuItem variant="destructive" onClick={() => onDeleteClient(client)}>
          <Trash2Icon className="size-4" />
          <Trans>Delete</Trans>
        </ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>
  );
}

export function DesktopClientActionMenu({
  client,
  onSelectedClientsChange,
  onViewProfile,
  onManageClient,
  onDeleteClient
}: Readonly<ClientActionMenuProps>) {
  return (
    <DropdownMenu
      onOpenChange={(isOpen) => {
        if (isOpen) {
          onSelectedClientsChange([client]);
          trackInteraction("Client actions", "menu", "Open");
        }
      }}
    >
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="icon" tabIndex={-1} aria-label={t`Client actions`}>
            <EllipsisVerticalIcon className="size-5 text-muted-foreground" />
          </Button>
        }
      />
      <DropdownMenuContent className="w-auto">
        <DropdownMenuItem onClick={() => onViewProfile(client)}>
          <UserIcon className="size-4" />
          <Trans>View profile</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => onManageClient(client)}>
          <PencilIcon className="size-4" />
          <Trans>Manage client</Trans>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onClick={() => onDeleteClient(client)}>
          <Trash2Icon className="size-4" />
          <Trans>Delete</Trans>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

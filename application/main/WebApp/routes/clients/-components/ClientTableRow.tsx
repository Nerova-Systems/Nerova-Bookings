import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback, AvatarImage } from "@repo/ui/components/Avatar";
import { Badge } from "@repo/ui/components/Badge";
import { TableCell, TableRow } from "@repo/ui/components/Table";
import { getInitials } from "@repo/utils/string/getInitials";
import { AlertTriangleIcon } from "lucide-react";

import type { components } from "@/shared/lib/api/client";

import { SmartDate } from "@/shared/components/SmartDate";

import { DesktopClientActionMenu, MobileClientActionMenu } from "./ClientActionMenus";

type ClientDetails = components["schemas"]["ClientDetails"];

interface ClientTableRowProps {
  client: ClientDetails;
  isMobile: boolean;
  onSelectedClientsChange: (clients: ClientDetails[]) => void;
  onViewProfile: (client: ClientDetails) => void;
  onManageClient: (client: ClientDetails) => void;
  onDeleteClient: (client: ClientDetails) => void;
}

export function ClientTableRow({
  client,
  isMobile,
  onSelectedClientsChange,
  onViewProfile,
  onManageClient,
  onDeleteClient
}: Readonly<ClientTableRowProps>) {
  const displayName = `${client.firstName} ${client.lastName}`.trim();
  const clientRowContent = (
    <div className="flex h-14 w-full items-center justify-between gap-2 p-0">
      <div className="flex min-w-0 flex-1 items-center gap-2 text-left font-normal">
        <Avatar size="lg">
          <AvatarImage src={client.avatarUrl ?? undefined} />
          <AvatarFallback>{getInitials(client.firstName, client.lastName, client.email ?? "")}</AvatarFallback>
        </Avatar>
        <div className="flex min-w-0 flex-1 flex-col">
          <div className="flex items-center gap-2 truncate text-foreground">
            <span className="truncate">{displayName || (isMobile ? (client.email ?? client.phoneNumber) : "")}</span>
            {!isMobile && client.needsAttention && (
              <Badge variant="outline" className="shrink-0 gap-1 text-warning">
                <AlertTriangleIcon className="size-3" />
                <Trans>Needs info</Trans>
              </Badge>
            )}
          </div>
          {isMobile && client.needsAttention ? (
            <Badge variant="outline" className="mt-1 -ml-2 w-fit gap-1 text-warning">
              <AlertTriangleIcon className="size-3" />
              <Trans>Needs info</Trans>
            </Badge>
          ) : (
            <span className="block truncate text-sm text-muted-foreground">{client.email ?? ""}</span>
          )}
        </div>
      </div>
    </div>
  );

  const actionMenuProps = {
    client,
    onSelectedClientsChange,
    onViewProfile,
    onManageClient,
    onDeleteClient
  };

  return (
    <TableRow rowKey={client.id}>
      <TableCell className="pr-8">
        {isMobile ? (
          <MobileClientActionMenu {...actionMenuProps}>{clientRowContent}</MobileClientActionMenu>
        ) : (
          clientRowContent
        )}
      </TableCell>
      {!isMobile && (
        <>
          <TableCell>
            <span className="block h-full w-full justify-start truncate p-0 text-left font-normal">
              {client.email ?? ""}
            </span>
          </TableCell>
          <TableCell>
            <span className="block h-full w-full justify-start truncate p-0 text-left font-normal">
              {client.phoneNumber ?? ""}
            </span>
          </TableCell>
          <TableCell>
            <SmartDate date={client.createdAt} className="text-foreground" />
          </TableCell>
          <TableCell>
            <div className="flex h-full w-full items-center justify-between p-0">
              <SmartDate date={client.lastVisitAt} className="text-foreground" />
              <DesktopClientActionMenu {...actionMenuProps} />
            </div>
          </TableCell>
        </>
      )}
    </TableRow>
  );
}

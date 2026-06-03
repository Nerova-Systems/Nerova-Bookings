import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { SidePane, SidePaneBody, SidePaneFooter, SidePaneHeader } from "@repo/ui/components/SidePane";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { PencilIcon, Trash2Icon } from "lucide-react";

import type { components } from "@/shared/lib/api/client";

import { ClientProfileContent } from "./ClientProfileContent";

type ClientDetails = components["schemas"]["ClientDetails"];

interface ClientProfileSidePaneProps {
  client: ClientDetails | null;
  isOpen: boolean;
  onClose: () => void;
  onManageClient: (client: ClientDetails) => void;
  onDeleteClient: (client: ClientDetails) => void;
  isLoading?: boolean;
}

export function ClientProfileSidePane({
  client,
  isOpen,
  onClose,
  onManageClient,
  onDeleteClient,
  isLoading = false
}: Readonly<ClientProfileSidePaneProps>) {
  return (
    <SidePane
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      trackingTitle="Client profile"
      trackingKey={client?.id}
      aria-label={t`Client profile`}
    >
      <SidePaneHeader closeButtonLabel={t`Close client profile`}>
        <Trans>Client profile</Trans>
      </SidePaneHeader>

      <SidePaneBody>
        {isLoading ? (
          <>
            <div className="mb-6 text-center">
              <Skeleton className="mx-auto mb-3 size-20 rounded-full" />
              <Skeleton className="mx-auto mb-2 h-6 w-32" />
              <Skeleton className="mx-auto h-4 w-24" />
            </div>
            <Skeleton className="h-64 w-full" />
          </>
        ) : (
          client && <ClientProfileContent client={client} />
        )}
      </SidePaneBody>

      {client && (
        <SidePaneFooter>
          <div className="flex w-full gap-2">
            <Button variant="default" onClick={() => onManageClient(client)} className="flex-1 justify-center text-sm">
              <PencilIcon className="size-4" />
              <Trans>Manage client</Trans>
            </Button>
            <Button
              variant="destructive"
              onClick={() => onDeleteClient(client)}
              className="flex-1 justify-center text-sm"
            >
              <Trash2Icon className="size-4" />
              <Trans>Delete client</Trans>
            </Button>
          </div>
        </SidePaneFooter>
      )}
    </SidePane>
  );
}

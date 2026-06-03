import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { Trash2Icon } from "lucide-react";
import { useState } from "react";

import type { components } from "@/shared/lib/api/client";

import { ClientQuerying } from "./ClientQuerying";
import { DeleteClientDialog } from "./DeleteClientDialog";

type ClientDetails = components["schemas"]["ClientDetails"];

interface ClientToolbarProps {
  selectedClients: ClientDetails[];
  onSelectedClientsChange: (clients: ClientDetails[]) => void;
}

export function ClientToolbar({ selectedClients, onSelectedClientsChange }: Readonly<ClientToolbarProps>) {
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);

  const buttonExpandClass = "@[30rem]:w-fit @[30rem]:gap-1.5 @[30rem]:px-6";
  const textVisibilityClass = "hidden @[30rem]:inline";

  return (
    <div className="@container mb-4 flex items-center justify-between gap-2">
      <ClientQuerying
        onFiltersUpdated={() => onSelectedClientsChange([])}
      />
      <div className="mt-auto flex items-center gap-2">
        {selectedClients.length > 1 && (
          <Tooltip>
            <TooltipTrigger
              render={
                <Button
                  variant="destructive"
                  size="icon"
                  className={buttonExpandClass}
                  onClick={() => setIsDeleteModalOpen(true)}
                  aria-label={t`Delete ${selectedClients.length} clients`}
                >
                  <Trash2Icon className="size-5" />
                  <span className={textVisibilityClass}>
                    <Trans>Delete {selectedClients.length} clients</Trans>
                  </span>
                </Button>
              }
            />
            <TooltipContent>
              <Trans>Delete clients</Trans>
            </TooltipContent>
          </Tooltip>
        )}
      </div>
      <DeleteClientDialog
        clients={selectedClients}
        isOpen={isDeleteModalOpen}
        onOpenChange={setIsDeleteModalOpen}
        onClientsDeleted={() => onSelectedClientsChange([])}
      />
    </div>
  );
}

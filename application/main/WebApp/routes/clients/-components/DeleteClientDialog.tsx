import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogMedia,
  AlertDialogTitle
} from "@repo/ui/components/AlertDialog";
import { useQueryClient } from "@tanstack/react-query";
import { Trash2Icon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api, type components } from "@/shared/lib/api/client";

type ClientDetails = components["schemas"]["ClientDetails"];

interface DeleteClientDialogProps {
  clients: ClientDetails[];
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onClientsDeleted?: () => void;
}

export function DeleteClientDialog({
  clients,
  isOpen,
  onOpenChange,
  onClientsDeleted
}: Readonly<DeleteClientDialogProps>) {
  const isSingleClient = clients.length === 1;
  const client = clients[0];
  const queryClient = useQueryClient();

  const deleteClientMutation = api.useMutation("delete", "/api/main/clients/{id}", {
    meta: { skipQueryInvalidation: true }
  });
  const bulkDeleteClientsMutation = api.useMutation("post", "/api/main/clients/bulk-delete", {
    meta: { skipQueryInvalidation: true }
  });
  const clientDisplayName = isSingleClient
    ? `${client.firstName ?? ""} ${client.lastName ?? ""}`.trim() || client.email || client.phoneNumber || ""
    : "";

  const [isPending, setIsPending] = useState(false);

  const handleDelete = async () => {
    setIsPending(true);
    try {
      if (isSingleClient) {
        await deleteClientMutation.mutateAsync({ params: { path: { id: client.id } } });
        toast.success(t`Client deleted successfully: ${clientDisplayName}`);
      } else {
        const clientIds = clients.map((c) => c.id);
        await bulkDeleteClientsMutation.mutateAsync({ body: { clientIds: clientIds } });
        toast.success(t`${clients.length} clients deleted successfully`);
      }

      await queryClient.invalidateQueries({
        predicate: (query) => {
          const key = query.queryKey;
          return Array.isArray(key) && key[0] === "get" && key[1] === "/api/main/clients";
        }
      });

      onClientsDeleted?.();
      onOpenChange(false);
    } finally {
      setIsPending(false);
    }
  };

  return (
    <AlertDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Delete client">
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogMedia className="bg-destructive/10">
            <Trash2Icon className="text-destructive" />
          </AlertDialogMedia>
          <AlertDialogTitle>{isSingleClient ? t`Delete client` : t`Delete clients`}</AlertDialogTitle>
          <AlertDialogDescription>
            {isSingleClient ? (
              <Trans>
                Are you sure you want to delete <b>{clientDisplayName}</b>?
              </Trans>
            ) : (
              <Trans>
                Are you sure you want to delete <b>{clients.length} clients</b>?
              </Trans>
            )}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel variant="secondary" disabled={isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction variant="destructive" isPending={isPending} onClick={handleDelete}>
            <Trans>Delete</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

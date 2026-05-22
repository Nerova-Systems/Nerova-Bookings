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

import { api, type Schemas } from "@/shared/lib/api/client";

type RoleResponse = Schemas["RoleResponse"];

interface DeleteRoleDialogProps {
  role: RoleResponse | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onDeleted?: () => void;
}

export function DeleteRoleDialog({ role, isOpen, onOpenChange, onDeleted }: Readonly<DeleteRoleDialogProps>) {
  const queryClient = useQueryClient();
  const [isPending, setIsPending] = useState(false);
  const deleteRoleMutation = api.useMutation("delete", "/api/account/roles/{id}", {
    meta: { skipQueryInvalidation: true }
  });

  const handleDelete = async () => {
    if (!role) {
      return;
    }
    setIsPending(true);
    try {
      await deleteRoleMutation.mutateAsync({ params: { path: { id: role.id } } });
      await queryClient.invalidateQueries({
        predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/roles"
      });
      toast.success(t`Role deleted: ${role.name}`);
      onDeleted?.();
      onOpenChange(false);
    } finally {
      setIsPending(false);
    }
  };

  return (
    <AlertDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Delete role">
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogMedia className="bg-destructive/10">
            <Trash2Icon className="text-destructive" />
          </AlertDialogMedia>
          <AlertDialogTitle>
            <Trans>Delete role</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            {role && (
              <Trans>
                Are you sure you want to delete <b>{role.name}</b>? Members assigned to this role will lose its
                permissions.
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

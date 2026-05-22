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
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { useQueryClient } from "@tanstack/react-query";
import { Trash2Icon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

type TeamResponse = Schemas["TeamResponse"];

interface DeleteTeamDialogProps {
  team: TeamResponse | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onDeleted?: () => void;
}

export function DeleteTeamDialog({ team, isOpen, onOpenChange, onDeleted }: Readonly<DeleteTeamDialogProps>) {
  const queryClient = useQueryClient();
  const [confirmName, setConfirmName] = useState("");
  const [isPending, setIsPending] = useState(false);

  // Reset confirmation input each time the dialog reopens for a (potentially different) team.
  useEffect(() => {
    if (isOpen) {
      setConfirmName("");
    }
  }, [isOpen]);

  const deleteTeamMutation = api.useMutation("delete", "/api/account/teams/{id}", {
    meta: { skipQueryInvalidation: true }
  });

  const handleDelete = async () => {
    if (!team) {
      return;
    }
    setIsPending(true);
    try {
      await deleteTeamMutation.mutateAsync({ params: { path: { id: team.id } } });
      await queryClient.invalidateQueries({
        predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/teams"
      });
      toast.success(t`Team deleted: ${team.name}`);
      onDeleted?.();
      onOpenChange(false);
    } finally {
      setIsPending(false);
    }
  };

  const confirmed = team !== null && confirmName.trim() === team.name;

  return (
    <AlertDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Delete team">
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogMedia className="bg-destructive/10">
            <Trash2Icon className="text-destructive" />
          </AlertDialogMedia>
          <AlertDialogTitle>
            <Trans>Delete team</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            {team && (
              <Trans>
                Deleting <b>{team.name}</b> will remove all memberships and is irreversible. Type the team name to
                confirm.
              </Trans>
            )}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <div className="flex flex-col gap-2">
          <Label htmlFor="confirm-team-name">
            <Trans>Team name</Trans>
          </Label>
          <Input
            id="confirm-team-name"
            value={confirmName}
            onChange={(e) => setConfirmName(e.target.value)}
            disabled={isPending}
            aria-label={t`Confirm team name`}
            placeholder={team?.name ?? ""}
          />
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel variant="secondary" disabled={isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            isPending={isPending}
            onClick={handleDelete}
            disabled={!confirmed || isPending}
          >
            <Trans>Delete</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

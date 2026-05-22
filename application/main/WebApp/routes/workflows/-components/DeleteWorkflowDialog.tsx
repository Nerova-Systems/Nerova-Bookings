import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogBody,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from "@repo/ui/components/AlertDialog";
import { TextField } from "@repo/ui/components/TextField";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { Workflow } from "./workflowTypes";

import { WorkflowApiErrors } from "./WorkflowApiErrors";
import { workflowPathId } from "./workflowTypes";

export function DeleteWorkflowDialog({
  workflow,
  isOpen,
  onOpenChange,
  onDeleted
}: Readonly<{
  workflow: Workflow | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onDeleted?: () => void;
}>) {
  const [confirmation, setConfirmation] = useState("");
  const deleteMutation = api.useMutation("delete", "/api/workflows/{id}", {
    onSuccess: () => {
      toast.success(t`Workflow deleted`);
      void queryClient.invalidateQueries();
      onOpenChange(false);
      onDeleted?.();
    }
  });

  const handleOpenChange = (open: boolean) => {
    if (!open) {
      setConfirmation("");
      deleteMutation.reset();
    }
    onOpenChange(open);
  };

  const expectedName = workflow?.name ?? "";
  const canConfirm = !!workflow && confirmation.trim() === expectedName.trim();

  return (
    <AlertDialog trackingTitle={t`Delete workflow`} open={isOpen} onOpenChange={handleOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            <Trans>Delete workflow?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>
              This removes the workflow "{expectedName}" and every reminder it schedules. Type the workflow name to
              confirm.
            </Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogBody>
          <WorkflowApiErrors error={deleteMutation.error} />
          <TextField
            name="workflowNameConfirmation"
            label={t`Workflow name`}
            value={confirmation}
            onChange={setConfirmation}
            autoFocus={true}
            disabled={deleteMutation.isPending}
          />
        </AlertDialogBody>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={deleteMutation.isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            isPending={deleteMutation.isPending}
            disabled={!canConfirm}
            onClick={() => {
              if (!workflow) return;
              deleteMutation.mutate({ params: { path: { id: workflowPathId(workflow.id) } } });
            }}
          >
            <Trans>Delete</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { ComboboxField } from "@repo/ui/components/ComboboxField";
import {
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { TextField } from "@repo/ui/components/TextField";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

import { api, MembershipRole, type Schemas } from "@/shared/lib/api/client";

type MembershipRoleType = Schemas["MembershipRole"];

interface InviteTeamMemberDialogProps {
  teamId: string;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

export function InviteTeamMemberDialog({ teamId, isOpen, onOpenChange }: Readonly<InviteTeamMemberDialogProps>) {
  const handleClose = () => onOpenChange(false);

  return (
    <DirtyDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Invite team member">
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Invite team member</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>The user must already have an account. Pick the role they will hold in this team.</Trans>
          </DialogDescription>
        </DialogHeader>
        <InviteTeamMemberDialogBody teamId={teamId} onClose={handleClose} />
      </DialogContent>
    </DirtyDialog>
  );
}

function InviteTeamMemberDialogBody({ teamId, onClose }: { teamId: string; onClose: () => void }) {
  const setDirty = useDialogSetDirty();
  const queryClient = useQueryClient();
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<MembershipRoleType>(MembershipRole.Member);
  const [customRoleId, setCustomRoleId] = useState<string | null>(null);

  const { data: roles } = api.useQuery("get", "/api/account/roles");

  const inviteMutation = api.useMutation("post", "/api/account/teams/{id}/invitations", {
    meta: { skipQueryInvalidation: true },
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        predicate: (query) =>
          Array.isArray(query.queryKey) &&
          typeof query.queryKey[1] === "string" &&
          query.queryKey[1].startsWith("/api/account/teams")
      });
      toast.success(t`Invitation sent`);
      onClose();
    }
  });

  const handleRoleChange = (value: string | null) => {
    setRole((value as MembershipRoleType | null) ?? MembershipRole.Member);
    setDirty(true);
  };

  const handleCustomRoleChange = (value: string | null) => {
    setCustomRoleId(value);
    setDirty(true);
  };

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    inviteMutation.mutate({
      params: { path: { id: teamId } },
      body: {
        email: email.trim(),
        role,
        customRoleId: customRoleId === "" ? null : customRoleId
      }
    });
  };

  return (
    <DialogForm onSubmit={handleSubmit} validationErrors={inviteMutation.error?.errors}>
      <DialogBody>
        <TextField
          autoFocus={true}
          required={true}
          name="email"
          type="email"
          label={t`Email`}
          placeholder={t`user@email.com`}
          value={email}
          onChange={(value) => {
            setEmail(value);
            setDirty(true);
          }}
        />
        <ComboboxField
          name="role"
          label={t`Membership role`}
          value={role}
          onValueChange={handleRoleChange}
          items={[
            { id: MembershipRole.Owner, label: t`Owner` },
            { id: MembershipRole.Admin, label: t`Admin` },
            { id: MembershipRole.Member, label: t`Member` }
          ]}
        />
        <ComboboxField
          name="customRoleId"
          label={t`Custom role`}
          value={customRoleId}
          onValueChange={handleCustomRoleChange}
          items={(roles ?? []).map((r) => ({ id: r.id, label: r.name }))}
          placeholder={t`None`}
          emptyMessage={<Trans>No custom roles available</Trans>}
        />
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={inviteMutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={inviteMutation.isPending}>
          {inviteMutation.isPending ? <Trans>Sending...</Trans> : <Trans>Send invite</Trans>}
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

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
import { Button } from "@repo/ui/components/Button";
import { useQueryClient } from "@tanstack/react-query";
import { Trash2Icon, UserPlusIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

import { InviteTeamMemberDialog } from "./InviteTeamMemberDialog";
import { MembersTable } from "./MembersTable";

type TeamMemberResponse = Schemas["TeamMemberResponse"];

interface TeamMembersTabProps {
  teamId: string;
  canManage: boolean;
}

export function TeamMembersTab({ teamId, canManage }: Readonly<TeamMembersTabProps>) {
  const queryClient = useQueryClient();
  const [isInviteOpen, setInviteOpen] = useState(false);
  const [memberToRemove, setMemberToRemove] = useState<TeamMemberResponse | null>(null);

  const { data: members, isLoading } = api.useQuery("get", "/api/account/teams/{id}/members", {
    params: { path: { id: teamId } }
  });

  const { data: roles } = api.useQuery("get", "/api/account/roles");

  const assignRoleMutation = api.useMutation("put", "/api/account/memberships/{id}/role", {
    meta: { skipQueryInvalidation: true }
  });
  const removeMutation = api.useMutation("delete", "/api/account/memberships/{id}", {
    meta: { skipQueryInvalidation: true }
  });

  const invalidateMembers = async () => {
    await queryClient.invalidateQueries({
      predicate: (query) =>
        Array.isArray(query.queryKey) &&
        typeof query.queryKey[1] === "string" &&
        query.queryKey[1].startsWith("/api/account/teams")
    });
  };

  const handleAssignRole = async (member: TeamMemberResponse, roleId: string | null) => {
    await assignRoleMutation.mutateAsync(
      {
        params: { path: { id: member.membershipId } },
        body: { roleId }
      },
      {
        onSuccess: async () => {
          await invalidateMembers();
          toast.success(t`Custom role updated`);
        }
      }
    );
  };

  const handleRemove = async () => {
    if (!memberToRemove) {
      return;
    }
    await removeMutation.mutateAsync(
      { params: { path: { id: memberToRemove.membershipId } } },
      {
        onSuccess: async () => {
          await invalidateMembers();
          toast.success(t`Member removed`);
          setMemberToRemove(null);
        }
      }
    );
  };

  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <div className="flex justify-end">
          <Button onClick={() => setInviteOpen(true)}>
            <UserPlusIcon />
            <Trans>Invite member</Trans>
          </Button>
        </div>
      )}

      <MembersTable
        members={members}
        roles={roles ?? []}
        isLoading={isLoading}
        canManage={canManage}
        isAssigning={assignRoleMutation.isPending}
        onAssignRole={handleAssignRole}
        onRequestRemove={setMemberToRemove}
      />

      <InviteTeamMemberDialog teamId={teamId} isOpen={isInviteOpen} onOpenChange={setInviteOpen} />

      <AlertDialog
        open={memberToRemove !== null}
        onOpenChange={(open) => !open && setMemberToRemove(null)}
        trackingTitle="Remove team member"
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogMedia className="bg-destructive/10">
              <Trash2Icon className="text-destructive" />
            </AlertDialogMedia>
            <AlertDialogTitle>
              <Trans>Remove member</Trans>
            </AlertDialogTitle>
            <AlertDialogDescription>
              {memberToRemove && (
                <Trans>
                  Remove <b>{memberToRemove.email}</b> from this team? They will lose access to team event types and
                  bookings.
                </Trans>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel variant="secondary" disabled={removeMutation.isPending}>
              <Trans>Cancel</Trans>
            </AlertDialogCancel>
            <AlertDialogAction variant="destructive" isPending={removeMutation.isPending} onClick={handleRemove}>
              <Trans>Remove</Trans>
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback, AvatarImage } from "@repo/ui/components/Avatar";
import { Badge } from "@repo/ui/components/Badge";
import { ComboboxField } from "@repo/ui/components/ComboboxField";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api, type Schemas, UserRole } from "@/shared/lib/api/client";

type UserDetails = Schemas["UserDetails"];

interface OrgMembersTabProps {
  canManage: boolean;
}

export function OrgMembersTab({ canManage }: Readonly<OrgMembersTabProps>) {
  const queryClient = useQueryClient();

  // Pull the first page of users — pagination UI is out of scope for this hub.
  // Bulk management lives at `/account/users` (the dedicated Users view).
  const { data, isLoading } = api.useQuery("get", "/api/account/users", {
    params: { query: { PageSize: 100 } }
  });

  const changeRoleMutation = api.useMutation("put", "/api/account/users/{id}/change-user-role", {
    meta: { skipQueryInvalidation: true }
  });

  const handleChangeRole = async (user: UserDetails, role: UserRole) => {
    if (user.role === role) {
      return;
    }
    await changeRoleMutation.mutateAsync(
      {
        params: { path: { id: user.id } },
        body: { userRole: role }
      },
      {
        onSuccess: async () => {
          await queryClient.invalidateQueries({
            predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/users"
          });
          toast.success(t`Role updated for ${user.email}`);
        }
      }
    );
  };

  if (isLoading) {
    return null;
  }

  const users = data?.users ?? [];
  if (users.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        <Trans>No members in this organization yet.</Trans>
      </p>
    );
  }

  const roleItems = [
    { id: UserRole.Member, label: t`Member` },
    { id: UserRole.Admin, label: t`Admin` },
    { id: UserRole.Owner, label: t`Owner` }
  ];

  return (
    <Table rowSize="compact">
      <TableHeader>
        <TableRow>
          <TableHead>
            <Trans>Member</Trans>
          </TableHead>
          <TableHead>
            <Trans>Role</Trans>
          </TableHead>
          <TableHead>
            <Trans>Status</Trans>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {users.map((user) => {
          const displayName = [user.firstName, user.lastName].filter(Boolean).join(" ") || user.email;
          return (
            <TableRow key={user.id}>
              <TableCell>
                <div className="flex items-center gap-2">
                  <Avatar className="size-8">
                    {user.avatarUrl && <AvatarImage src={user.avatarUrl} alt={user.email} />}
                    <AvatarFallback>{user.email.charAt(0).toUpperCase()}</AvatarFallback>
                  </Avatar>
                  <div className="flex flex-col">
                    <span className="font-medium">{displayName}</span>
                    <span className="text-sm text-muted-foreground">{user.email}</span>
                  </div>
                </div>
              </TableCell>
              <TableCell>
                {canManage ? (
                  <ComboboxField
                    value={user.role}
                    onValueChange={(value) => value && handleChangeRole(user, value as UserRole)}
                    items={roleItems}
                    disabled={changeRoleMutation.isPending}
                  />
                ) : (
                  <Badge variant="secondary">{user.role}</Badge>
                )}
              </TableCell>
              <TableCell>
                {user.emailConfirmed ? (
                  <Badge variant="outline">
                    <Trans>Active</Trans>
                  </Badge>
                ) : (
                  <Badge variant="secondary">
                    <Trans>Pending</Trans>
                  </Badge>
                )}
              </TableCell>
              {/* TODO(u4-org-settings): per-user team badges, impersonate-action, and
                  remove-from-org actions are scoped out. The dedicated /account/users page
                  already handles delete; impersonation requires a super-admin flow that
                  isn't in scope for this slice. */}
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

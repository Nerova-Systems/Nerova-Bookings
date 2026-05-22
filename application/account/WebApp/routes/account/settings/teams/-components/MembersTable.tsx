import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback, AvatarImage } from "@repo/ui/components/Avatar";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { ComboboxField } from "@repo/ui/components/ComboboxField";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { Trash2Icon } from "lucide-react";

import { type Schemas } from "@/shared/lib/api/client";

type TeamMemberResponse = Schemas["TeamMemberResponse"];
type RoleResponse = Schemas["RoleResponse"];

interface MembersTableProps {
  members: TeamMemberResponse[] | undefined;
  roles: RoleResponse[];
  isLoading: boolean;
  canManage: boolean;
  isAssigning: boolean;
  onAssignRole: (member: TeamMemberResponse, roleId: string | null) => void;
  onRequestRemove: (member: TeamMemberResponse) => void;
}

export function MembersTable({
  members,
  roles,
  isLoading,
  canManage,
  isAssigning,
  onAssignRole,
  onRequestRemove
}: Readonly<MembersTableProps>) {
  if (isLoading) {
    return null;
  }

  if (!members || members.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        <Trans>No members yet. Invite your first teammate to get started.</Trans>
      </p>
    );
  }

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
            <Trans>Custom role</Trans>
          </TableHead>
          <TableHead>
            <Trans>Status</Trans>
          </TableHead>
          {canManage && <TableHead className="w-px text-right" />}
        </TableRow>
      </TableHeader>
      <TableBody>
        {members.map((member) => (
          <MemberRow
            key={member.membershipId}
            member={member}
            roles={roles}
            canManage={canManage}
            isAssigning={isAssigning}
            onAssignRole={onAssignRole}
            onRequestRemove={onRequestRemove}
          />
        ))}
      </TableBody>
    </Table>
  );
}

interface MemberRowProps {
  member: TeamMemberResponse;
  roles: RoleResponse[];
  canManage: boolean;
  isAssigning: boolean;
  onAssignRole: (member: TeamMemberResponse, roleId: string | null) => void;
  onRequestRemove: (member: TeamMemberResponse) => void;
}

function MemberRow({ member, roles, canManage, isAssigning, onAssignRole, onRequestRemove }: Readonly<MemberRowProps>) {
  const displayName = [member.firstName, member.lastName].filter(Boolean).join(" ") || member.email;
  return (
    <TableRow>
      <TableCell>
        <div className="flex items-center gap-2">
          <Avatar className="size-8">
            {member.avatarUrl && <AvatarImage src={member.avatarUrl} alt={member.email} />}
            <AvatarFallback>{member.email.charAt(0).toUpperCase()}</AvatarFallback>
          </Avatar>
          <div className="flex flex-col">
            <span className="font-medium">{displayName}</span>
            <span className="text-sm text-muted-foreground">{member.email}</span>
          </div>
        </div>
      </TableCell>
      <TableCell>
        <Badge variant="secondary">{member.role}</Badge>
      </TableCell>
      <TableCell>
        {canManage ? (
          <ComboboxField
            value={member.customRoleId}
            onValueChange={(value) => onAssignRole(member, value)}
            items={roles.map((r) => ({ id: r.id, label: r.name }))}
            placeholder={t`None`}
            emptyMessage={<Trans>No custom roles available</Trans>}
            disabled={isAssigning}
          />
        ) : (
          <span className="text-sm text-muted-foreground">
            {roles.find((r) => r.id === member.customRoleId)?.name ?? "—"}
          </span>
        )}
      </TableCell>
      <TableCell>
        {member.accepted ? (
          <Badge variant="outline">
            <Trans>Active</Trans>
          </Badge>
        ) : (
          <Badge variant="secondary">
            <Trans>Pending</Trans>
          </Badge>
        )}
      </TableCell>
      {canManage && (
        <TableCell className="text-right">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onRequestRemove(member)}
            aria-label={t`Remove ${member.email}`}
          >
            <Trash2Icon className="size-4" />
          </Button>
        </TableCell>
      )}
    </TableRow>
  );
}

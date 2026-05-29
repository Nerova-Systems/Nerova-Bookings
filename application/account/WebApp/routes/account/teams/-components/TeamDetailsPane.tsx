import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback, AvatarImage } from "@repo/ui/components/Avatar";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import {
  Item,
  ItemActions,
  ItemContent,
  ItemDescription,
  ItemMedia,
  ItemTitle,
  ItemGroup
} from "@repo/ui/components/Item";
import { Separator } from "@repo/ui/components/Separator";
import { SidePane, SidePaneBody, SidePaneFooter, SidePaneHeader } from "@repo/ui/components/SidePane";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { PencilIcon, Trash2Icon, UserCogIcon } from "lucide-react";

import { api, type Schemas } from "@/shared/lib/api/client";

type Team = Schemas["TeamResponse"];
type TeamMemberResponse = Schemas["TeamMemberResponse"];

interface TeamDetailsPaneProps {
  team: Team;
  isOpen: boolean;
  onClose: () => void;
  onEdit: () => void;
  onEditMembers: () => void;
  onDelete: () => void;
  canManage: boolean;
}

export function TeamDetailsPane({
  team,
  isOpen,
  onClose,
  onEdit,
  onEditMembers,
  onDelete,
  canManage
}: Readonly<TeamDetailsPaneProps>) {
  const formatDate = useFormatDate();

  // Load team members in-place for active details list
  const { data: members, isLoading: loadingMembers } = api.useQuery("get", "/api/account/teams/{id}/members", {
    params: { path: { id: team.id } }
  });

  const memberCount = members?.length ?? 0;

  return (
    <SidePane
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      trackingTitle="Team details"
      trackingKey={team.id}
      aria-label={t`Team details`}
    >
      <SidePaneHeader closeButtonLabel={t`Close team details`}>
        <Trans>Team details</Trans>
      </SidePaneHeader>

      <SidePaneBody>
        <div className="mb-6 flex flex-col gap-1">
          <h3 className="text-xl font-bold tracking-tight text-foreground">{team.name}</h3>
          <span className="text-xs text-muted-foreground">/{team.slug ?? "—"}</span>
          {team.bio && <p className="mt-3 line-clamp-3 text-sm text-muted-foreground">{team.bio}</p>}
        </div>

        <ItemGroup>
          <Item size="xs">
            <ItemContent>
              <ItemTitle className="text-sm font-normal text-muted-foreground">
                <Trans>Created</Trans>
              </ItemTitle>
            </ItemContent>
            <ItemActions>
              <span className="text-sm font-medium text-foreground">{formatDate(team.createdAt, true)}</span>
            </ItemActions>
          </Item>

          <Item size="xs">
            <ItemContent>
              <ItemTitle className="text-sm font-normal text-muted-foreground">
                <Trans>Members</Trans>
              </ItemTitle>
            </ItemContent>
            <ItemActions>
              <span className="text-brand font-mono text-sm font-semibold">{memberCount}</span>
            </ItemActions>
          </Item>
        </ItemGroup>

        <Separator className="my-6" />

        <div className="flex flex-col gap-3">
          <h4 className="text-sm font-semibold tracking-tight text-foreground uppercase">
            <Trans>Team members</Trans>
          </h4>
          {loadingMembers ? (
            <p className="text-xs text-muted-foreground">
              <Trans>Loading members...</Trans>
            </p>
          ) : !members || members.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              <Trans>No members yet.</Trans>
            </p>
          ) : (
            <div className="flex max-h-[16rem] flex-col gap-1 overflow-y-auto pr-1">
              {members.map((member) => (
                <TeamMemberRow key={member.membershipId} member={member} />
              ))}
            </div>
          )}
        </div>

        {canManage && (
          <Button variant="secondary" onClick={onEditMembers} className="mt-6 w-full justify-center">
            <UserCogIcon className="size-4" />
            <Trans>Manage members</Trans>
          </Button>
        )}
      </SidePaneBody>

      {canManage && (
        <SidePaneFooter className="flex flex-col gap-2">
          <Button variant="secondary" onClick={onEdit} className="w-full justify-center">
            <PencilIcon className="size-4" />
            <Trans>Edit general settings</Trans>
          </Button>
          <Button variant="destructive" onClick={onDelete} className="w-full justify-center">
            <Trash2Icon className="size-4" />
            <Trans>Delete team</Trans>
          </Button>
        </SidePaneFooter>
      )}
    </SidePane>
  );
}

function TeamMemberRow({ member }: { member: TeamMemberResponse }) {
  const displayName = [member.firstName, member.lastName].filter(Boolean).join(" ") || member.email;
  return (
    <Item size="sm" className="rounded-md p-2 hover:bg-muted/10">
      <ItemMedia variant="image" className="size-9">
        <Avatar className="size-9">
          {member.avatarUrl && <AvatarImage src={member.avatarUrl} alt={member.email} />}
          <AvatarFallback className="bg-brand/10 text-brand text-xs font-semibold uppercase">
            {member.email.charAt(0).toUpperCase()}
          </AvatarFallback>
        </Avatar>
      </ItemMedia>
      <ItemContent>
        <ItemTitle className="flex items-center gap-1.5 text-sm font-medium text-foreground">
          {displayName}
          {member.role === "Owner" && (
            <Badge variant="secondary" className="px-1.5 py-0 font-mono text-[10px] uppercase">
              <Trans>Owner</Trans>
            </Badge>
          )}
        </ItemTitle>
        <ItemDescription className="text-xs text-muted-foreground">{member.email}</ItemDescription>
      </ItemContent>
      <ItemActions>
        {member.accepted ? (
          <Badge
            variant="outline"
            className="border-success/30 bg-success/5 px-1 py-0 font-mono text-[10px] text-success"
          >
            <Trans>Active</Trans>
          </Badge>
        ) : (
          <Badge variant="secondary" className="px-1 py-0 font-mono text-[10px]">
            <Trans>Pending</Trans>
          </Badge>
        )}
      </ItemActions>
    </Item>
  );
}

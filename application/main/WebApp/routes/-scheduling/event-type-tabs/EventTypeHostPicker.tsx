import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback } from "@repo/ui/components/Avatar";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { TextField } from "@repo/ui/components/TextField";
import { CheckIcon, UsersIcon, XIcon } from "lucide-react";
import { useMemo, useState } from "react";

import { api } from "@/shared/lib/api/client";

import type { EventTypeSettings } from "../schedulingTypes";

type HostGroup = EventTypeSettings["teamAssignment"]["hostGroups"][number];

const DefaultHostGroupId = "default";

type HostPickerProps = Readonly<{
  eventTypeId: string;
  hostGroups: HostGroup[];
  onChange: (hostGroups: HostGroup[]) => void;
}>;

export function EventTypeHostPicker({ eventTypeId, hostGroups, onChange }: HostPickerProps) {
  const [searchQuery, setSearchQuery] = useState("");

  const { data: candidatesData, isLoading: isLoadingCandidates } = api.useQuery(
    "get",
    "/api/event-types/{id}/assignment-candidates",
    { params: { path: { id: eventTypeId } } }
  );
  const { data: membersData, isLoading: isLoadingMembers } = api.useQuery("get", "/api/team-members/search", {
    params: { query: { query: searchQuery || null, limit: 100 } }
  });

  const candidateUserIds = useMemo(
    () => new Set((candidatesData?.candidates ?? []).map((candidate) => candidate.userId)),
    [candidatesData]
  );

  // Resolve display info for the selected user ids (so chips stay populated even when filtered out by search).
  const allMembers = membersData?.members ?? [];
  const memberDirectory = useMemo(() => {
    const directory = new Map<string, { displayName: string; email: string }>();
    for (const member of membersData?.members ?? []) {
      directory.set(member.userId, { displayName: member.displayName, email: member.email });
    }
    return directory;
  }, [membersData?.members]);

  const selectedUserIds = useMemo(() => {
    const userIds = new Set<string>();
    for (const group of hostGroups) {
      for (const userId of group.memberUserIds) userIds.add(userId);
    }
    return userIds;
  }, [hostGroups]);

  const candidateMembers = allMembers.filter((member) => candidateUserIds.has(member.userId));
  const isLoading = isLoadingCandidates || isLoadingMembers;

  const setSelectedUserIds = (next: Set<string>) => {
    const userIds = Array.from(next);
    if (userIds.length === 0) {
      onChange([]);
      return;
    }
    // Collapse all hosts into a single default group. Backend supports multiple named groups, but the picker
    // exposes a single flat membership to match the cal.com EventTeamTab UX.
    const existing = hostGroups.find((group) => group.id === DefaultHostGroupId);
    const nextGroup: HostGroup = {
      id: DefaultHostGroupId,
      name: existing?.name ?? "Hosts",
      memberUserIds: userIds
    };
    onChange([nextGroup]);
  };

  const toggleUser = (userId: string) => {
    const next = new Set(selectedUserIds);
    if (next.has(userId)) {
      next.delete(userId);
    } else {
      next.add(userId);
    }
    setSelectedUserIds(next);
  };

  const removeUser = (userId: string) => {
    const next = new Set(selectedUserIds);
    next.delete(userId);
    setSelectedUserIds(next);
  };

  const selectedChips = Array.from(selectedUserIds).map((userId) => {
    const info = memberDirectory.get(userId);
    return { userId, displayName: info?.displayName ?? userId, email: info?.email };
  });

  return (
    <div className="grid gap-3">
      {selectedChips.length === 0 ? (
        <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
          <Trans>No hosts selected. Pick team members below to assign them to this event type.</Trans>
        </div>
      ) : (
        <ul className="flex flex-wrap gap-2" aria-label={t`Selected hosts`}>
          {selectedChips.map((chip) => (
            <li key={chip.userId}>
              <Badge variant="secondary" className="gap-1.5 pr-1 pl-2">
                <span className="text-xs">{chip.displayName}</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-xs"
                  aria-label={t`Remove host ${chip.displayName}`}
                  onClick={() => removeUser(chip.userId)}
                >
                  <XIcon className="h-3 w-3" aria-hidden="true" />
                </Button>
              </Badge>
            </li>
          ))}
        </ul>
      )}

      <TextField
        name="hostSearch"
        label={t`Search team members`}
        value={searchQuery}
        onChange={setSearchQuery}
        placeholder={t`Search by name or email`}
      />

      {isLoading ? (
        <div className="text-sm text-muted-foreground">
          <Trans>Loading hosts…</Trans>
        </div>
      ) : candidateMembers.length === 0 ? (
        <div className="flex items-center gap-2 rounded-md border p-3 text-sm text-muted-foreground">
          <UsersIcon className="h-4 w-4" aria-hidden="true" />
          {searchQuery ? (
            <Trans>No team members match your search.</Trans>
          ) : (
            <Trans>No hosts available to assign.</Trans>
          )}
        </div>
      ) : (
        <ul className="grid max-h-72 gap-1 overflow-y-auto rounded-md border p-1" aria-label={t`Available hosts`}>
          {candidateMembers.map((member) => {
            const isSelected = selectedUserIds.has(member.userId);
            return (
              <li key={member.userId}>
                <button
                  type="button"
                  onClick={() => toggleUser(member.userId)}
                  aria-pressed={isSelected}
                  className="flex w-full items-center gap-3 rounded-sm px-2 py-2 text-left hover:bg-accent hover:text-accent-foreground aria-pressed:bg-accent/60"
                >
                  <Avatar size="sm">
                    <AvatarFallback>{getInitials(member.displayName)}</AvatarFallback>
                  </Avatar>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-medium">{member.displayName}</div>
                    <div className="truncate text-xs text-muted-foreground">{member.email}</div>
                  </div>
                  {isSelected ? (
                    <CheckIcon className="h-4 w-4 text-primary" aria-hidden="true" />
                  ) : (
                    <span aria-hidden="true" className="h-4 w-4" />
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

function getInitials(displayName: string) {
  const trimmed = displayName.trim();
  if (trimmed.length === 0) return "?";
  const parts = trimmed.split(/\s+/);
  const first = parts[0]?.[0] ?? "";
  const last = parts.length > 1 ? (parts.at(-1)?.[0] ?? "") : "";
  return (first + last).toUpperCase();
}

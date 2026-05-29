import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Avatar, AvatarFallback, AvatarImage } from "@repo/ui/components/Avatar";
import { buttonVariants } from "@repo/ui/components/Button";
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { PlusIcon, UsersIcon } from "lucide-react";
import { useState } from "react";
import { z } from "zod";

import { api, type Schemas, UserRole } from "@/shared/lib/api/client";

import { CreateTeamDialog } from "./-components/CreateTeamDialog";
import { DeleteTeamDialog } from "./-components/DeleteTeamDialog";
import { EditTeamDialog } from "./-components/EditTeamDialog";
import { EditTeamMembersDialog } from "./-components/EditTeamMembersDialog";
import { TeamDetailsPane } from "./-components/TeamDetailsPane";

type Team = Schemas["TeamResponse"];

const teamsPageSearchSchema = z.object({
  teamId: z.string().optional()
});

export const Route = createFileRoute("/account/teams/")({
  beforeLoad: () => {
    // The tier-teams feature flag gates the whole Teams surface.
    if (!isFeatureFlagEnabled("tier-teams")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Teams" },
  component: TeamsPage,
  validateSearch: teamsPageSearchSchema
});

function TeamsPage() {
  const userInfo = useUserInfo();
  const navigate = useNavigate({ from: Route.fullPath });
  const { teamId } = Route.useSearch();
  
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [teamToEdit, setTeamToEdit] = useState<Team | null>(null);
  const [teamToEditMembers, setTeamToEditMembers] = useState<Team | null>(null);
  const [teamToDelete, setTeamToDelete] = useState<Team | null>(null);

  const canManageTeams = userInfo?.role === UserRole.Owner || userInfo?.role === UserRole.Admin;

  const { data: teams, isLoading } = api.useQuery("get", "/api/account/teams");

  const openDetails = (team: Team) => {
    navigate({ search: (prev) => ({ ...prev, teamId: team.id }) });
  };

  const closeDetails = () => {
    navigate({ search: (prev) => ({ ...prev, teamId: undefined }) });
  };

  const selectedTeam = teamId && teams ? (teams.find((team) => team.id === teamId) ?? null) : null;

  const sidePane = selectedTeam ? (
    <TeamDetailsPane
      team={selectedTeam}
      isOpen={true}
      onClose={closeDetails}
      onEdit={() => setTeamToEdit(selectedTeam)}
      onEditMembers={() => setTeamToEditMembers(selectedTeam)}
      onDelete={() => setTeamToDelete(selectedTeam)}
      canManage={canManageTeams}
    />
  ) : undefined;

  return (
    <>
      <AppLayout
        variant="center"
        maxWidth="64rem"
        title={t`Teams`}
        subtitle={t`Group members into teams to share event types, schedules, and round-robin assignments.`}
        sidePane={sidePane}
      >
        {canManageTeams && teams && teams.length > 0 && (
          <div className="flex justify-end mb-4">
            <button
              onClick={() => setIsCreateOpen(true)}
              className={buttonVariants({ variant: "default" })}
              aria-label={t`New team`}
            >
              <PlusIcon className="size-4" />
              <Trans>New team</Trans>
            </button>
          </div>
        )}

        {isLoading ? null : !teams || teams.length === 0 ? (
          <Empty>
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <UsersIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No teams yet</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>Create a team to collaborate on event types and bookings with your colleagues.</Trans>
              </EmptyDescription>
            </EmptyHeader>
            {canManageTeams && (
              <EmptyContent>
                <button
                  onClick={() => setIsCreateOpen(true)}
                  className={buttonVariants({ variant: "default" })}
                  aria-label={t`Create a team`}
                >
                  <PlusIcon className="size-4" />
                  <Trans>Create a team</Trans>
                </button>
              </EmptyContent>
            )}
          </Empty>
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {teams.map((team) => (
              <div
                key={team.id}
                onClick={() => openDetails(team)}
                className="linear-card flex flex-col justify-between p-6 transition-all hover:border-muted-foreground/30 hover:bg-muted/10 group relative cursor-pointer"
              >
                <div>
                  <div className="flex items-start justify-between">
                    <div className="flex items-center gap-3">
                      <Avatar className="size-10 border border-border">
                        {team.logoUrl && <AvatarImage src={team.logoUrl} alt={team.name} />}
                        <AvatarFallback className="bg-brand/10 text-brand font-semibold text-sm">
                          {team.name.charAt(0).toUpperCase()}
                        </AvatarFallback>
                      </Avatar>
                      <div>
                        <h4 className="font-semibold text-foreground tracking-tight transition-colors group-hover:text-brand">
                          {team.name}
                        </h4>
                        <span className="text-xs text-muted-foreground">/{team.slug ?? "—"}</span>
                      </div>
                    </div>
                  </div>

                  {team.bio && (
                    <p className="mt-4 text-sm text-muted-foreground line-clamp-2">
                      {team.bio}
                    </p>
                  )}
                </div>

                <div className="mt-6 flex items-center justify-between border-t border-border/50 pt-4">
                  <div className="flex flex-col">
                    <span className="text-[10px] text-muted-foreground uppercase tracking-wider font-semibold">
                      <Trans>Members</Trans>
                    </span>
                    <span className="font-mono text-xl font-bold tracking-tight text-foreground mt-0.5">
                      {team.memberCount}
                    </span>
                  </div>

                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      openDetails(team);
                    }}
                    className={buttonVariants({ variant: "ghost", size: "sm" })}
                    aria-label={t`Manage ${team.name}`}
                  >
                    <Trans>Manage</Trans>
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </AppLayout>

      <CreateTeamDialog
        isOpen={isCreateOpen}
        onOpenChange={setIsCreateOpen}
        onSuccessRedirect={(createdTeamId) => {
          setIsCreateOpen(false);
          navigate({ search: { teamId: createdTeamId } });
        }}
      />

      <EditTeamDialog
        team={teamToEdit}
        isOpen={teamToEdit !== null}
        onOpenChange={(open) => !open && setTeamToEdit(null)}
      />

      <EditTeamMembersDialog
        team={teamToEditMembers}
        isOpen={teamToEditMembers !== null}
        onOpenChange={(open) => !open && setTeamToEditMembers(null)}
      />

      <DeleteTeamDialog
        team={teamToDelete}
        isOpen={teamToDelete !== null}
        onOpenChange={(open) => !open && setTeamToDelete(null)}
        onDeleted={() => {
          setTeamToDelete(null);
          closeDetails();
        }}
      />
    </>
  );
}

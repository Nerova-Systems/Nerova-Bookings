import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Avatar, AvatarFallback, AvatarImage } from "@repo/ui/components/Avatar";
import { Badge } from "@repo/ui/components/Badge";
import { buttonVariants } from "@repo/ui/components/Button";
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { PlusIcon, UsersIcon } from "lucide-react";

import { api } from "@/shared/lib/api/client";

export const Route = createFileRoute("/account/settings/teams/")({
  beforeLoad: () => {
    // The tier-teams feature flag gates the whole Teams surface. The sidebar hides the entry when
    // disabled, but direct navigation must redirect rather than render an unsupported page.
    if (!isFeatureFlagEnabled("tier-teams")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Teams" },
  component: TeamsPage
});

function TeamsPage() {
  const userInfo = useUserInfo();
  const canManageTeams = userInfo?.role === "Owner" || userInfo?.role === "Admin";

  const { data: teams, isLoading } = api.useQuery("get", "/api/account/teams");

  return (
    <AppLayout
      variant="center"
      maxWidth="64rem"
      title={t`Teams`}
      subtitle={t`Create teams to group members and share event types, schedules, and bookings.`}
    >
      {canManageTeams && (
        <div className="flex justify-end">
          <Link to="/account/settings/teams/new" className={buttonVariants({})} aria-label={t`New team`}>
            <PlusIcon />
            <Trans>New team</Trans>
          </Link>
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
              <Link to="/account/settings/teams/new" className={buttonVariants({})} aria-label={t`Create a team`}>
                <PlusIcon />
                <Trans>Create a team</Trans>
              </Link>
            </EmptyContent>
          )}
        </Empty>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {teams.map((team) => (
            <div
              key={team.id}
              className="linear-card flex flex-col justify-between p-6 transition-all hover:border-muted-foreground/30 hover:bg-muted/10 group relative"
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

                <Link
                  to="/account/settings/teams/$teamId"
                  params={{ teamId: team.id }}
                  className={buttonVariants({ variant: "ghost", size: "sm" })}
                  aria-label={t`Manage ${team.name}`}
                >
                  <Trans>Manage</Trans>
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}
    </AppLayout>
  );
}

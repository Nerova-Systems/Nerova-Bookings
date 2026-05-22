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
          <Link
            to="/account/settings/teams/new"
            className={buttonVariants({ variant: "default" })}
            aria-label={t`New team`}
          >
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
              <Link
                to="/account/settings/teams/new"
                className={buttonVariants({ variant: "default" })}
                aria-label={t`Create a team`}
              >
                <PlusIcon />
                <Trans>Create a team</Trans>
              </Link>
            </EmptyContent>
          )}
        </Empty>
      ) : (
        <Table rowSize="compact">
          <TableHeader>
            <TableRow>
              <TableHead>
                <Trans>Name</Trans>
              </TableHead>
              <TableHead>
                <Trans>Slug</Trans>
              </TableHead>
              <TableHead className="w-32">
                <Trans>Members</Trans>
              </TableHead>
              <TableHead className="w-px text-right" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {teams.map((team) => (
              <TableRow key={team.id}>
                <TableCell>
                  <div className="flex items-center gap-2">
                    <Avatar className="size-8">
                      {team.logoUrl && <AvatarImage src={team.logoUrl} alt={team.name} />}
                      <AvatarFallback>{team.name.charAt(0).toUpperCase()}</AvatarFallback>
                    </Avatar>
                    <span className="font-medium">{team.name}</span>
                  </div>
                </TableCell>
                <TableCell className="text-muted-foreground">{team.slug ?? "—"}</TableCell>
                <TableCell>
                  <Badge variant="outline">{team.memberCount}</Badge>
                </TableCell>
                <TableCell className="text-right">
                  <Link
                    to="/account/settings/teams/$teamId"
                    params={{ teamId: team.id }}
                    className={buttonVariants({ variant: "ghost", size: "sm" })}
                    aria-label={t`Manage ${team.name}`}
                  >
                    <Trans>Manage</Trans>
                  </Link>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </AppLayout>
  );
}

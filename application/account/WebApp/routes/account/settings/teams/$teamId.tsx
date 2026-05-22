import { Trans } from "@lingui/react/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { isFeatureFlagEnabled, useFeatureFlag } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@repo/ui/components/Tabs";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { api } from "@/shared/lib/api/client";

import { DeleteTeamDialog } from "./-components/DeleteTeamDialog";
import { TeamGeneralTab } from "./-components/TeamGeneralTab";
import { TeamMembersTab } from "./-components/TeamMembersTab";

export const Route = createFileRoute("/account/settings/teams/$teamId")({
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("tier-teams")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Team settings" },
  component: TeamDetailPage
});

function TeamDetailPage() {
  const { teamId } = Route.useParams();
  const navigate = useNavigate();
  const userInfo = useUserInfo();
  const { enabled: isAttributesEnabled } = useFeatureFlag("cap-attributes");
  const [isDeleteOpen, setDeleteOpen] = useState(false);

  const canManage = userInfo?.role === "Owner" || userInfo?.role === "Admin";

  const { data: team, isLoading } = api.useQuery("get", "/api/account/teams/{id}", {
    params: { path: { id: teamId } }
  });

  if (isLoading || !team) {
    return null;
  }

  return (
    <>
      <AppLayout variant="center" maxWidth="64rem" title={team.name} subtitle={team.bio ?? undefined}>
        <Tabs defaultValue="general">
          <TabsList>
            <TabsTrigger value="general">
              <Trans>General</Trans>
            </TabsTrigger>
            <TabsTrigger value="members">
              <Trans>Members</Trans>
            </TabsTrigger>
            {isAttributesEnabled && (
              <TabsTrigger value="attributes">
                <Trans>Attributes</Trans>
              </TabsTrigger>
            )}
          </TabsList>

          <TabsContent value="general">
            <TeamGeneralTab team={team} canManage={canManage} onDelete={() => setDeleteOpen(true)} />
          </TabsContent>

          <TabsContent value="members">
            <TeamMembersTab teamId={team.id} canManage={canManage} />
          </TabsContent>

          {isAttributesEnabled && (
            <TabsContent value="attributes">
              <p className="text-sm text-muted-foreground">
                <Trans>Member attributes are coming soon.</Trans>
              </p>
            </TabsContent>
          )}
        </Tabs>
      </AppLayout>

      <DeleteTeamDialog
        team={team}
        isOpen={isDeleteOpen}
        onOpenChange={setDeleteOpen}
        onDeleted={() => navigate({ to: "/account/settings/teams" })}
      />
    </>
  );
}

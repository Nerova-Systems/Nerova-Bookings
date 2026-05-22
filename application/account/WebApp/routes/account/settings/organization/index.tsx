import { Trans } from "@lingui/react/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { isFeatureFlagEnabled, useFeatureFlag } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@repo/ui/components/Tabs";
import { createFileRoute, redirect } from "@tanstack/react-router";

import { api } from "@/shared/lib/api/client";

import { OrgBillingTab } from "./-components/BillingTab";
import { OrgMembersTab } from "./-components/MembersTab";
import { OrgProfileTab } from "./-components/ProfileTab";
import { OrgSmtpTab } from "./-components/SmtpTab";
import { OrgSsoTab } from "./-components/SsoTab";
import { OrgTeamsTab } from "./-components/TeamsTab";

export const Route = createFileRoute("/account/settings/organization/")({
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("tier-organizations")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Organization settings" },
  component: OrganizationSettingsPage
});

function OrganizationSettingsPage() {
  const userInfo = useUserInfo();
  const { enabled: isSsoEnabled } = useFeatureFlag("sso");
  const { enabled: isSmtpEnabled } = useFeatureFlag("cap-custom-smtp");
  const { enabled: isBillingEnabled } = useFeatureFlag("cap-org-billing");

  const canManage = userInfo?.role === "Owner" || userInfo?.role === "Admin";

  const { data: tenant, isLoading } = api.useQuery("get", "/api/account/tenants/current");

  if (isLoading || !tenant) {
    return null;
  }

  if (!canManage) {
    return (
      <AppLayout variant="center" maxWidth="64rem" title={<Trans>Organization</Trans>}>
        <p className="text-sm text-muted-foreground">
          <Trans>You don't have permission to view organization settings.</Trans>
        </p>
      </AppLayout>
    );
  }

  return (
    <AppLayout variant="center" maxWidth="64rem" title={tenant.name} subtitle={<Trans>Organization settings</Trans>}>
      <Tabs defaultValue="profile">
        <TabsList>
          <TabsTrigger value="profile">
            <Trans>Profile</Trans>
          </TabsTrigger>
          <TabsTrigger value="members">
            <Trans>Members</Trans>
          </TabsTrigger>
          <TabsTrigger value="teams">
            <Trans>Teams</Trans>
          </TabsTrigger>
          {isSsoEnabled && (
            <TabsTrigger value="sso">
              <Trans>SSO</Trans>
            </TabsTrigger>
          )}
          {isSmtpEnabled && (
            <TabsTrigger value="smtp">
              <Trans>SMTP</Trans>
            </TabsTrigger>
          )}
          {isBillingEnabled && (
            <TabsTrigger value="billing">
              <Trans>Billing</Trans>
            </TabsTrigger>
          )}
        </TabsList>

        <TabsContent value="profile">
          <OrgProfileTab tenant={tenant} canManage={canManage} />
        </TabsContent>

        <TabsContent value="members">
          <OrgMembersTab canManage={canManage} />
        </TabsContent>

        <TabsContent value="teams">
          <OrgTeamsTab />
        </TabsContent>

        {isSsoEnabled && (
          <TabsContent value="sso">
            <OrgSsoTab canManage={canManage} />
          </TabsContent>
        )}

        {isSmtpEnabled && (
          <TabsContent value="smtp">
            <OrgSmtpTab canManage={canManage} />
          </TabsContent>
        )}

        {isBillingEnabled && (
          <TabsContent value="billing">
            <OrgBillingTab />
          </TabsContent>
        )}
      </Tabs>
    </AppLayout>
  );
}

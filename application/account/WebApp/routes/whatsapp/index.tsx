import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@repo/ui/components/Tabs";
import { createFileRoute } from "@tanstack/react-router";
import {
  BarChart3Icon,
  MessageSquareIcon,
  SettingsIcon,
  UserCircleIcon,
  ZapIcon,
} from "lucide-react";

import { ProfileTab } from "./-components/ProfileTab";
import { SetupTab } from "./-components/SetupTab";
import { UsageTab } from "./-components/UsageTab";
import { WorkflowsTab } from "./-components/WorkflowsTab";

export const Route = createFileRoute("/whatsapp/")({
  staticData: { trackingTitle: "WhatsApp setup" },
  component: WhatsappPage
});

function WhatsappPage() {
  return (
    <AppLayout
      variant="center"
      maxWidth="56rem"
      browserTitle={t`WhatsApp`}
      title=""
      subtitle=""
    >
      {/* Page header */}
      <div className="mb-6">
        <div className="mb-1.5 flex items-center gap-2">
          <div className="flex size-8 items-center justify-center rounded-lg bg-green-500 shadow-lg shadow-green-500/25">
            <MessageSquareIcon className="size-4 text-white" />
          </div>
          <span className="text-xs font-bold uppercase tracking-widest text-green-600 dark:text-green-400">
            WhatsApp Integration
          </span>
        </div>

        <h1 className="text-2xl font-extrabold leading-tight">
          <Trans>WhatsApp Business</Trans>
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          <Trans>
            Manage your WhatsApp Business Account, configure your profile, and control booking workflows.
          </Trans>
        </p>
      </div>

      <Tabs defaultValue="setup">
        <TabsList className="mb-6">
          <TabsTrigger value="setup">
            <SettingsIcon className="mr-1.5 size-3.5" />
            <Trans>Setup</Trans>
          </TabsTrigger>
          <TabsTrigger value="profile">
            <UserCircleIcon className="mr-1.5 size-3.5" />
            <Trans>Profile</Trans>
          </TabsTrigger>
          <TabsTrigger value="workflows">
            <ZapIcon className="mr-1.5 size-3.5" />
            <Trans>Workflows</Trans>
          </TabsTrigger>
          <TabsTrigger value="usage">
            <BarChart3Icon className="mr-1.5 size-3.5" />
            <Trans>Usage</Trans>
          </TabsTrigger>
        </TabsList>

        <TabsContent value="setup">
          <SetupTab />
        </TabsContent>

        <TabsContent value="profile">
          <ProfileTab />
        </TabsContent>

        <TabsContent value="workflows">
          <WorkflowsTab />
        </TabsContent>

        <TabsContent value="usage">
          <UsageTab />
        </TabsContent>
      </Tabs>
    </AppLayout>
  );
}

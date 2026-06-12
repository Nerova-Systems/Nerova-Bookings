import { t } from "@lingui/core/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute, redirect } from "@tanstack/react-router";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";
import { isWhatsAppSignupEnabled } from "@/shared/lib/whatsapp/whatsAppConfig";

import { ReceptionistCard } from "./-components/ReceptionistCard";
import { WhatsAppConnectionCard } from "./-components/WhatsAppConnectionCard";
import { WhatsAppConversation } from "./-components/WhatsAppConversation";
import { WhatsAppConversationsPanel } from "./-components/WhatsAppConversationsPanel";
import { WhatsAppStats } from "./-components/WhatsAppStats";
import { WhatsAppWebhookActivityPanel } from "./-components/WhatsAppWebhookActivityPanel";

export const Route = createFileRoute("/channels/whatsapp")({
  beforeLoad: () => {
    // Hide the whole feature when the public toggle is off. The side-menu entry is also gated, but
    // the route stays reachable via direct URL or a stale bookmark, so redirect to the dashboard.
    if (!isWhatsAppSignupEnabled) {
      throw redirect({ to: "/dashboard" });
    }
  },
  staticData: { trackingTitle: "WhatsApp" },
  component: WhatsAppPage
});

function WhatsAppPage() {
  const statusQuery = api.useQuery("get", "/api/main/whatsapp/status");
  const isConnected = statusQuery.data?.isConnected ?? false;

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="48rem"
          browserTitle={t`WhatsApp`}
          title={t`WhatsApp`}
          subtitle={t`Connect your WhatsApp Business account to send and receive messages.`}
        >
          <div className="flex flex-col gap-8">
            <WhatsAppConnectionCard />
            {isConnected && <ReceptionistCard />}
            {isConnected && <WhatsAppStats />}
            {isConnected && <WhatsAppWebhookActivityPanel />}
            {isConnected && <WhatsAppConversationsPanel />}
            {isConnected && <WhatsAppConversation />}
          </div>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

import { t } from "@lingui/core/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute } from "@tanstack/react-router";
import { MailIcon, MessageCircleIcon, SmartphoneIcon } from "lucide-react";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";
import { isWhatsAppSignupEnabled } from "@/shared/lib/whatsapp/whatsAppConfig";

import { ChannelCard } from "./-components/ChannelCard";

export const Route = createFileRoute("/channels/")({
  staticData: { trackingTitle: "Channels" },
  component: ChannelsOverviewPage
});

function ChannelsOverviewPage() {
  const statusQuery = api.useQuery("get", "/api/main/whatsapp/status", {}, { enabled: isWhatsAppSignupEnabled });
  const isWhatsAppConnected = statusQuery.data?.isConnected ?? false;

  const whatsAppStatus = !isWhatsAppSignupEnabled
    ? "coming-soon"
    : statusQuery.isLoading
      ? "loading"
      : isWhatsAppConnected
        ? "connected"
        : "available";

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="72rem"
          browserTitle={t`Channels`}
          title={t`Channels`}
          subtitle={t`Connect the messaging channels your customers use to receive bookings and reminders.`}
        >
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            <ChannelCard
              icon={<MessageCircleIcon className="size-6 text-emerald-600 dark:text-emerald-400" />}
              name={t`WhatsApp Business`}
              description={t`Reach customers on WhatsApp with embedded signup, business profile setup, and booking workflows.`}
              status={whatsAppStatus}
              accentClassName="bg-gradient-to-br from-emerald-500/15 to-green-500/5"
              to="/channels/whatsapp"
            />
            <ChannelCard
              icon={<SmartphoneIcon className="size-6 text-muted-foreground" />}
              name={t`SMS`}
              description={t`Send booking confirmations and reminders over SMS. Coming soon.`}
              status="coming-soon"
            />
            <ChannelCard
              icon={<MailIcon className="size-6 text-muted-foreground" />}
              name={t`Email`}
              description={t`Deliver transactional booking emails to your customers. Coming soon.`}
              status="coming-soon"
            />
          </div>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

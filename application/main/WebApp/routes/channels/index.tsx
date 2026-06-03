import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { Button, buttonVariants } from "@repo/ui/components/Button";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { Link as RouterLink, createFileRoute } from "@tanstack/react-router";
import { CheckCircle2Icon, MailIcon, MessageCircleIcon, SmartphoneIcon } from "lucide-react";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";
import { isWhatsAppSignupEnabled } from "@/shared/lib/whatsapp/whatsAppConfig";

export const Route = createFileRoute("/channels/")({
  staticData: { trackingTitle: "Channels" },
  component: ChannelsOverviewPage
});

function ChannelsOverviewPage() {
  const statusQuery = api.useQuery("get", "/api/main/whatsapp/status", {}, { enabled: isWhatsAppSignupEnabled });
  const isWhatsAppConnected = statusQuery.data?.isConnected ?? false;

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
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <ChannelCard
              icon={<MessageCircleIcon className="size-6 text-emerald-600 dark:text-emerald-400" />}
              name={t`WhatsApp Business`}
              description={t`Reach customers on WhatsApp with embedded signup, business profile setup, and booking workflows.`}
              isAvailable={isWhatsAppSignupEnabled}
              isConnected={isWhatsAppConnected}
              isLoading={isWhatsAppSignupEnabled && statusQuery.isLoading}
              to="/channels/whatsapp"
            />
            <ChannelCard
              icon={<SmartphoneIcon className="size-6 text-muted-foreground" />}
              name={t`SMS`}
              description={t`Send booking confirmations and reminders over SMS. Coming soon.`}
              isAvailable={false}
              isConnected={false}
              isLoading={false}
            />
            <ChannelCard
              icon={<MailIcon className="size-6 text-muted-foreground" />}
              name={t`Email`}
              description={t`Deliver transactional booking emails to your customers. Coming soon.`}
              isAvailable={false}
              isConnected={false}
              isLoading={false}
            />
          </div>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

interface ChannelCardProps {
  icon: React.ReactNode;
  name: string;
  description: string;
  isAvailable: boolean;
  isConnected: boolean;
  isLoading: boolean;
  to?: "/channels/whatsapp";
}

function ChannelCard({ icon, name, description, isAvailable, isConnected, isLoading, to }: Readonly<ChannelCardProps>) {
  return (
    <div className="relative flex h-56 flex-col rounded-md border border-border bg-card p-5">
      <div className="absolute top-4 right-4">
        {isLoading ? (
          <Badge variant="outline" className="text-muted-foreground">
            <Trans>Checking…</Trans>
          </Badge>
        ) : isConnected ? (
          <Badge className="flex items-center gap-1 border-emerald-500/20 bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
            <CheckCircle2Icon className="size-3" />
            <Trans>Connected</Trans>
          </Badge>
        ) : isAvailable ? (
          <Badge variant="outline" className="text-muted-foreground">
            <Trans>Not connected</Trans>
          </Badge>
        ) : (
          <Badge variant="outline" className="text-muted-foreground">
            <Trans>Coming soon</Trans>
          </Badge>
        )}
      </div>

      <div className="mb-4 flex size-12 items-center justify-center rounded-sm border bg-background">{icon}</div>

      <h3 className="font-medium text-foreground">{name}</h3>
      <p className="mt-2 line-clamp-3 text-sm text-muted-foreground">{description}</p>

      <div className="mt-auto">
        {isAvailable && to ? (
          <RouterLink
            to={to}
            className={buttonVariants({
              variant: isConnected ? "outline" : "default",
              size: "sm",
              className: "justify-center"
            })}
          >
            {isConnected ? <Trans>Manage</Trans> : <Trans>Connect</Trans>}
          </RouterLink>
        ) : (
          <Button variant="outline" size="sm" disabled={true} className="justify-center">
            <Trans>Coming soon</Trans>
          </Button>
        )}
      </div>
    </div>
  );
}

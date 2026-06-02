import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { createFileRoute, Link as RouterLink } from "@tanstack/react-router";
import {
  AlertCircleIcon,
  CheckCircle2Icon,
  KeyRoundIcon,
  MessageSquareIcon,
  PhoneIcon,
  SettingsIcon,
  WalletIcon
} from "lucide-react";

import { api } from "@/shared/lib/api/client";

import { DisplayNameStatusBadge } from "../account/settings/-components/DisplayNameDialog";

export const Route = createFileRoute("/channels/overview")({
  staticData: { trackingTitle: "Channels overview" },
  component: ChannelsOverviewPage
});

type Gate = {
  done: boolean;
  icon: React.ReactNode;
  label: React.ReactNode;
  action: React.ReactNode;
};

function ChannelsOverviewPage() {
  const { data: status } = api.useQuery("get", "/api/whatsapp/onboarding-status");
  const { data: displayName } = api.useQuery("get", "/api/whatsapp/display-name");

  // Treat a missing configuration (null/undefined) as "no gate satisfied".
  const wabaLinked = Boolean(status?.wabaLinked);
  const phoneRegistered = Boolean(status?.phoneRegistered);
  const keyPairGenerated = Boolean(status?.keyPairGenerated);
  const paystackConnected = Boolean(status?.paystackConnected);
  const canPublishFlow = Boolean(status?.canPublishFlow);

  const gates: Gate[] = [
    {
      done: wabaLinked,
      icon: <MessageSquareIcon className="size-4" />,
      label: <Trans>WhatsApp Business Account linked</Trans>,
      action: <Trans>Link WhatsApp Business Account</Trans>
    },
    {
      done: phoneRegistered,
      icon: <PhoneIcon className="size-4" />,
      label: <Trans>Phone number registered</Trans>,
      action: <Trans>Register phone number</Trans>
    },
    {
      done: keyPairGenerated,
      icon: <KeyRoundIcon className="size-4" />,
      label: <Trans>Encryption keys generated</Trans>,
      action: <Trans>Generate encryption keys</Trans>
    },
    {
      done: paystackConnected,
      icon: <WalletIcon className="size-4" />,
      label: <Trans>Paystack connected</Trans>,
      action: <Trans>Connect Paystack</Trans>
    }
  ];

  const requiredActions = gates.filter((gate) => !gate.done);

  return (
    <AppLayout
      variant="center"
      maxWidth="56rem"
      browserTitle={t`Channels`}
      title={t`Channels`}
      subtitle={t`Connect and manage the messaging channels your customers book through.`}
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Card className="gap-4">
          <CardHeader>
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-center gap-3">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-green-500 shadow-lg shadow-green-500/25">
                  <MessageSquareIcon className="size-5 text-white" />
                </div>
                <div>
                  <CardTitle>
                    <Trans>WhatsApp Business</Trans>
                  </CardTitle>
                  {status?.displayPhoneNumber && (
                    <p className="text-sm text-muted-foreground">{status.displayPhoneNumber}</p>
                  )}
                </div>
              </div>
              {canPublishFlow ? (
                <Badge variant="default">
                  <Trans>Connected</Trans>
                </Badge>
              ) : wabaLinked ? (
                <Badge variant="warning">
                  <Trans>Setup in progress</Trans>
                </Badge>
              ) : (
                <Badge variant="outline">
                  <Trans>Not connected</Trans>
                </Badge>
              )}
            </div>
          </CardHeader>

          <CardContent className="flex flex-col gap-4">
            {/* Connection checklist derived from the real onboarding gates. */}
            <ul className="flex flex-col gap-2">
              {gates.map((gate, index) => (
                <li key={index} className="flex items-center gap-2 text-sm">
                  {gate.done ? (
                    <CheckCircle2Icon className="size-4 shrink-0 text-green-500" />
                  ) : (
                    <AlertCircleIcon className="size-4 shrink-0 text-muted-foreground" />
                  )}
                  <span className={gate.done ? "" : "text-muted-foreground"}>{gate.label}</span>
                </li>
              ))}
            </ul>

            {/* WhatsApp display name + Meta review state (real data). */}
            <div className="flex flex-col gap-1.5 rounded-lg border border-border bg-muted/30 p-3">
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
                  <Trans>Display name</Trans>
                </span>
                <DisplayNameStatusBadge status={displayName} />
              </div>
              <span className="text-sm font-medium">
                {displayName?.verifiedName ?? displayName?.requestedDisplayName ?? (
                  <span className="text-muted-foreground">
                    <Trans>No verified name</Trans>
                  </span>
                )}
              </span>
            </div>

            {/* Required actions (only the gates that are still false), or a ready state. */}
            {requiredActions.length > 0 ? (
              <div className="flex flex-col gap-2">
                <span className="text-xs font-bold tracking-wider text-muted-foreground uppercase">
                  <Trans>Required actions</Trans>
                </span>
                <ul className="flex flex-col gap-1.5">
                  {requiredActions.map((gate, index) => (
                    <li key={index} className="flex items-center gap-2 text-sm text-muted-foreground">
                      <span className="text-amber-600 dark:text-amber-400">{gate.icon}</span>
                      {gate.action}
                    </li>
                  ))}
                </ul>
              </div>
            ) : (
              <div className="flex items-center gap-2 rounded-lg border border-green-300 bg-green-50/60 p-3 text-sm dark:border-green-800 dark:bg-green-950/30">
                <CheckCircle2Icon className="size-4 shrink-0 text-green-500" />
                <Trans>Your WhatsApp channel is fully connected and ready.</Trans>
              </div>
            )}

            <Button render={<RouterLink to="/channels/whatsapp" />} className="w-full">
              <SettingsIcon className="size-4" />
              <Trans>Manage WhatsApp</Trans>
            </Button>
          </CardContent>
        </Card>
      </div>
    </AppLayout>
  );
}

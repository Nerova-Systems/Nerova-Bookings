import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { MessageCircleIcon } from "lucide-react";
import { useCallback } from "react";
import { toast } from "sonner";

import type { EmbeddedSignupPayload } from "@/shared/lib/whatsapp/useWhatsAppEmbeddedSignup";

import { api, queryClient } from "@/shared/lib/api/client";
import { useWhatsAppEmbeddedSignup } from "@/shared/lib/whatsapp/useWhatsAppEmbeddedSignup";
import { whatsAppSignupConfig } from "@/shared/lib/whatsapp/whatsAppConfig";

const STATUS_QUERY_KEY = ["get", "/api/main/whatsapp/status"] as const;

function ConnectedDetails({
  businessName,
  phoneNumber,
  status
}: Readonly<{ businessName: string | null; phoneNumber: string | null; status: string }>) {
  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-4">
        <span className="text-sm text-muted-foreground">
          <Trans>Status</Trans>
        </span>
        <Badge variant="default">{status}</Badge>
      </div>
      <div className="flex items-center justify-between gap-4">
        <span className="text-sm text-muted-foreground">
          <Trans>Business name</Trans>
        </span>
        <span className="text-sm font-medium">{businessName ?? "-"}</span>
      </div>
      <div className="flex items-center justify-between gap-4">
        <span className="text-sm text-muted-foreground">
          <Trans>Phone number</Trans>
        </span>
        <span className="text-sm font-medium">{phoneNumber ?? "-"}</span>
      </div>
    </div>
  );
}

export function WhatsAppConnectionCard() {
  const statusQuery = api.useQuery("get", "/api/main/whatsapp/status");

  const completeMutation = api.useMutation("post", "/api/main/whatsapp/embedded-signup/complete", {
    onSuccess: () => {
      toast.success(t`WhatsApp connected`, {
        description: t`Your WhatsApp Business account is now connected.`
      });
      // Refresh the connection status so the card flips to the connected state.
      queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
    }
  });

  const handleComplete = useCallback(
    (payload: EmbeddedSignupPayload) => {
      completeMutation.mutate({ body: payload });
    },
    [completeMutation]
  );

  const { launch, isSdkReady, isLaunching } = useWhatsAppEmbeddedSignup({
    appId: whatsAppSignupConfig.metaAppId,
    configId: whatsAppSignupConfig.metaConfigId,
    onComplete: handleComplete
  });

  if (statusQuery.isLoading) {
    return <Skeleton className="h-48 w-full" />;
  }

  const status = statusQuery.data;
  const isConnected = status?.isConnected ?? false;
  const isBusy = isLaunching || completeMutation.isPending;

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-3">
          <div className="flex size-10 items-center justify-center rounded-lg bg-muted">
            <MessageCircleIcon className="size-5 text-muted-foreground" />
          </div>
          <div className="flex flex-col gap-1">
            <CardTitle>
              <Trans>WhatsApp Business</Trans>
            </CardTitle>
            <CardDescription>
              {isConnected ? (
                <Trans>Your account is connected to WhatsApp.</Trans>
              ) : (
                <Trans>Connect your WhatsApp Business account to start messaging.</Trans>
              )}
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {isConnected ? (
          <ConnectedDetails
            businessName={status?.businessName ?? null}
            phoneNumber={status?.phoneNumber ?? null}
            status={status?.status ?? ""}
          />
        ) : (
          <Button onClick={launch} disabled={!isSdkReady} isPending={isBusy}>
            {isBusy ? <Trans>Connecting...</Trans> : <Trans>Connect WhatsApp</Trans>}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

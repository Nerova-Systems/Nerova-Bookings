import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@repo/ui/components/Accordion";
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
    <Accordion>
      <AccordionItem value="technical-details" className="border-b-0">
        <AccordionTrigger className="py-2 text-sm">
          <Trans>Technical details</Trans>
        </AccordionTrigger>
        <AccordionContent>
          <dl className="grid gap-3 rounded-lg border bg-muted/30 p-4 text-sm sm:grid-cols-2">
            <div className="flex flex-col gap-1">
              <dt className="text-muted-foreground">
                <Trans>Connection state</Trans>
              </dt>
              <dd className="font-medium">{status || "-"}</dd>
            </div>
            <div className="flex flex-col gap-1">
              <dt className="text-muted-foreground">
                <Trans>Business name</Trans>
              </dt>
              <dd className="font-medium">{businessName ?? "-"}</dd>
            </div>
            <div className="flex flex-col gap-1">
              <dt className="text-muted-foreground">
                <Trans>Phone number</Trans>
              </dt>
              <dd className="font-medium">{phoneNumber ?? "-"}</dd>
            </div>
          </dl>
        </AccordionContent>
      </AccordionItem>
    </Accordion>
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

  const disconnectMutation = api.useMutation("delete", "/api/main/whatsapp/waba", {
    onSuccess: () => {
      toast.success(t`WhatsApp disconnected`, {
        description: t`Your WhatsApp Business account has been disconnected.`
      });
      queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
    }
  });

  const reprovisionMutation = api.useMutation("post", "/api/main/whatsapp/waba/reprovision-flows", {
    onSuccess: () => {
      toast.success(t`Booking experience fixed`, {
        description: t`Your WhatsApp booking and sign-in experience is up to date.`
      });
    },
    onError: () => {
      toast.error(t`Could not fix booking experience`, {
        description: t`Please try again or reconnect your WhatsApp account.`
      });
    }
  });

  const handleComplete = useCallback(
    (payload: EmbeddedSignupPayload) => {
      completeMutation.mutate({ body: payload });
    },
    [completeMutation]
  );

  const { launch, isSdkReady, isLaunching, sdkError } = useWhatsAppEmbeddedSignup({
    appId: whatsAppSignupConfig.metaAppId,
    configId: whatsAppSignupConfig.metaConfigId,
    onComplete: handleComplete
  });

  if (statusQuery.isLoading) {
    return <Skeleton className="h-48 w-full" />;
  }

  const status = statusQuery.data;
  const isConnected = status?.isConnected ?? false;
  const isBusy =
    isLaunching || completeMutation.isPending || disconnectMutation.isPending || reprovisionMutation.isPending;

  return (
    <Card className="border-primary/20 bg-primary/5">
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-3">
            <div className="flex size-10 items-center justify-center rounded-lg bg-muted">
              <MessageCircleIcon className="size-5 text-muted-foreground" />
            </div>
            <div className="flex flex-col gap-1">
              <CardTitle>
                {isConnected ? (
                  status?.phoneNumber ? (
                    <Trans>Nerova is answering WhatsApp on {status.phoneNumber}</Trans>
                  ) : (
                    <Trans>Nerova is answering WhatsApp</Trans>
                  )
                ) : (
                  <Trans>Nerova is not answering WhatsApp yet</Trans>
                )}
              </CardTitle>
              <CardDescription>
                {isConnected ? (
                  <Trans>Clients can message the number they already know. Nerova keeps the front desk moving.</Trans>
                ) : (
                  <Trans>Connect the number clients already message and we will guide them into bookings.</Trans>
                )}
              </CardDescription>
            </div>
          </div>
          <Badge variant={isConnected ? "default" : "secondary"}>
            {isConnected ? <Trans>Connected</Trans> : <Trans>Disconnected</Trans>}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {isConnected ? (
          <>
            <ConnectedDetails
              businessName={status?.businessName ?? null}
              phoneNumber={status?.phoneNumber ?? null}
              status={status?.status ?? ""}
            />
            <div className="flex justify-end gap-2">
              <Button
                variant="ghost"
                onClick={() => reprovisionMutation.mutate({})}
                isPending={reprovisionMutation.isPending}
              >
                <Trans>Fix booking experience</Trans>
              </Button>
              <Button
                variant="outline"
                onClick={() => disconnectMutation.mutate({})}
                isPending={disconnectMutation.isPending}
              >
                <Trans>Disconnect</Trans>
              </Button>
            </div>
          </>
        ) : (
          <div className="flex flex-col gap-2">
            <Button onClick={launch} disabled={!isSdkReady} isPending={isBusy}>
              {isBusy ? <Trans>Connecting...</Trans> : <Trans>Connect WhatsApp</Trans>}
            </Button>
            {sdkError ? (
              <p className="text-sm text-destructive">
                <Trans>Could not load WhatsApp sign-up. Please refresh and try again.</Trans>
              </p>
            ) : null}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

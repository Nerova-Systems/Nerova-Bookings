import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute, Link as RouterLink, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";

export const Route = createFileRoute("/whatsapp/preview")({
  staticData: { trackingTitle: "Flow preview" },
  component: PreviewPage
});

function PreviewPage() {
  const navigate = useNavigate();
  const { data: config } = api.useQuery("get", "/api/whatsapp-flows/config");

  // TODO(phase-5-followup): wire to GET /api/whatsapp-flows/preview once backend adds it
  const publishMutation = api.useMutation("post", "/api/whatsapp-flows/publish", {
    onSuccess: () => {
      toast.success(t`Flow published`);
      void navigate({ to: "/whatsapp/link" });
    }
  });

  const [publishResponse, setPublishResponse] = useState<{ flowId: string; status: string } | null>(null);

  const handlePublish = () => {
    publishMutation.mutate(
      { body: { businessName: null } },
      {
        onSuccess: (data) => {
          setPublishResponse({ flowId: data.flowId, status: data.status });
        }
      }
    );
  };

  const configEntries = config
    ? [
        { label: t`Business vertical`, value: config.businessVertical },
        { label: t`Default session (min)`, value: String(config.defaultSessionMinutes) },
        { label: t`Multiple services`, value: config.hasMultipleServices ? t`Yes` : t`No` },
        { label: t`Staff assignment`, value: config.staffAssignment },
        { label: t`Booking window (days)`, value: String(config.bookingWindowDays) },
        { label: t`Same-day bookings`, value: config.allowSameDayBookings ? t`Yes` : t`No` },
        { label: t`Payment timing`, value: config.paymentTiming },
        {
          label: t`Deposit amount`,
          value: config.depositAmountCents != null ? String(config.depositAmountCents) : t`N/A`
        },
        { label: t`Cancellation contact`, value: config.cancellationContact },
        { label: t`Confirmation template`, value: config.confirmationMessageTemplate }
      ]
    : [];

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout variant="center" maxWidth="48rem" browserTitle={t`Preview your flow`} title={t`Preview your flow`}>
          {config && (
            <div className="flex flex-col gap-4">
              <dl className="divide-y rounded-lg border">
                {configEntries.map((entry) => (
                  <div key={entry.label} className="flex flex-col gap-1 px-4 py-3 sm:flex-row sm:gap-4">
                    <dt className="min-w-[12rem] text-sm font-medium text-muted-foreground">{entry.label}</dt>
                    <dd className="text-sm">{entry.value}</dd>
                  </div>
                ))}
              </dl>
            </div>
          )}

          <div className="mt-6 flex flex-col gap-4">
            <Button variant="secondary" disabled={true}>
              <Trans>Open preview (coming soon)</Trans>
            </Button>
            <p className="text-sm text-muted-foreground">
              <Trans>Live preview is not yet available. Publish your flow to test it on your WhatsApp number.</Trans>
            </p>

            {publishResponse && (
              <p className="text-sm text-green-600">
                <Trans>Flow published — ID: {publishResponse.flowId}</Trans>
              </p>
            )}

            <div className="flex gap-3">
              <RouterLink to="/whatsapp/questionnaire" className="text-sm text-muted-foreground underline">
                <Trans>Back to questionnaire</Trans>
              </RouterLink>
              <Button onClick={handlePublish} isPending={publishMutation.isPending}>
                <Trans>Publish flow</Trans>
              </Button>
            </div>
          </div>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

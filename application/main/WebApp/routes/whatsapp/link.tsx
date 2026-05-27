import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute } from "@tanstack/react-router";
import { toast } from "sonner";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";

export const Route = createFileRoute("/whatsapp/link")({
  staticData: { trackingTitle: "Share your link" },
  component: LinkPage
});

function LinkPage() {
  // TODO(phase-5-followup): pull display phone number from account onboarding status (cross-SCS contract needed)
  const { data: _config } = api.useQuery("get", "/api/whatsapp-flows/config");

  const phoneDigits = "27000000000";
  const waUrl = `https://wa.me/${phoneDigits}?text=${encodeURIComponent("Book")}`;

  const handleCopy = () => {
    void navigator.clipboard.writeText(waUrl);
    toast.success(t`Link copied!`);
  };

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="48rem"
          browserTitle={t`Share your booking link`}
          title={t`Share your booking link`}
        >
          <Card>
            <CardHeader>
              <CardTitle>
                <Trans>Your WhatsApp booking link</Trans>
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex flex-col gap-4">
                <div className="flex items-center gap-2 rounded-md border bg-muted px-3 py-2">
                  <span className="flex-1 truncate font-mono text-sm">{waUrl}</span>
                  <Button variant="secondary" onClick={handleCopy}>
                    <Trans>Copy</Trans>
                  </Button>
                </div>
                <a
                  href={waUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex h-9 items-center justify-center rounded-md border border-input bg-background px-4 py-2 text-sm font-medium shadow-xs transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:ring-1 focus-visible:ring-ring focus-visible:outline-none"
                >
                  <Trans>Test the flow</Trans>
                </a>
              </div>
            </CardContent>
          </Card>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

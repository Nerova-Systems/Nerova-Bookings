import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute } from "@tanstack/react-router";

import { ComponentsSideMenu } from "./-components/ComponentsSideMenu";
import { EmailsPreview } from "./-components/EmailsPreview";
import { PreviewHeader } from "./-components/PreviewHeader";

export const Route = createFileRoute("/components/emails")({
  staticData: { trackingTitle: "Emails" },
  component: EmailsPage
});

function EmailsPage() {
  return (
    <SidebarProvider>
      <ComponentsSideMenu />
      <SidebarInset>
        <AppLayout
          variant="full"
          browserTitle={t`Emails`}
          title={<Trans>Emails</Trans>}
          beforeHeader={<PreviewHeader currentPage="emails" defaultTab="" tabLabels={{ "": <Trans>Emails</Trans> }} />}
        >
          <EmailsPreview />
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

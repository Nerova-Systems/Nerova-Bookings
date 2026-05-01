import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { BellIcon } from "lucide-react";
import { toast } from "sonner";

export const Route = createFileRoute("/user/push-notifications/")({
  staticData: { trackingTitle: "Push notifications" },
  component: PushNotificationsPage
});

function PushNotificationsPage() {
  const requestPermission = async () => {
    if (!("Notification" in window)) {
      toast.error(t`Browser notifications are not supported in this browser.`);
      return;
    }

    const permission = await Notification.requestPermission();
    if (permission === "granted") {
      toast.success(t`Browser notifications enabled.`);
    } else {
      toast.info(t`Browser notifications were not enabled.`);
    }
  };

  return (
    <AppLayout
      variant="center"
      maxWidth="72rem"
      balanceWidth="16rem"
      title={t`Push notifications`}
      subtitle={t`Receive push notifications when a booker submits an instant meeting booking.`}
    >
      <section className="mt-8 flex flex-wrap items-center gap-5 rounded-xl border border-border bg-card px-6 py-6">
        <div className="flex size-11 items-center justify-center rounded-xl bg-muted">
          <BellIcon className="size-5 text-muted-foreground" />
        </div>
        <div className="min-w-0 flex-1">
          <h2 className="text-lg font-semibold">
            <Trans>Browser notifications</Trans>
          </h2>
          <p className="mt-1 text-muted-foreground">
            <Trans>Manage whether this browser receives booking alerts.</Trans>
          </p>
        </div>
        <Button type="button" onClick={requestPermission}>
          <Trans>Allow browser notifications</Trans>
        </Button>
      </section>
    </AppLayout>
  );
}

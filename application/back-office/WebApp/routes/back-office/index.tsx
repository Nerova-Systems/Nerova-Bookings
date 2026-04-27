import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { createFileRoute, Link as RouterLink } from "@tanstack/react-router";
import { Building2Icon, InboxIcon, UsersIcon } from "lucide-react";

export const Route = createFileRoute("/back-office/")({
  staticData: { trackingTitle: "Back office" },
  component: Home
});

export default function Home() {
  return (
    <AppLayout
      browserTitle={t`Dashboard`}
      title={t`Back Office`}
      subtitle={t`Operate tenants and users from the synchronized platform catalog.`}
    >
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <RouterLink
          className="rounded-md border border-border p-4 outline-ring transition-colors hover:bg-muted focus-visible:outline-2 focus-visible:outline-offset-2"
          to="/back-office/tenants"
        >
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-md bg-muted">
              <Building2Icon className="size-5" />
            </div>
            <div className="min-w-0">
              <h2 className="text-base font-semibold">
                <Trans>Tenants</Trans>
              </h2>
              <p className="mt-1 text-sm text-muted-foreground">
                <Trans>Review catalog state and restore deleted tenants.</Trans>
              </p>
            </div>
          </div>
        </RouterLink>
        <RouterLink
          className="rounded-md border border-border p-4 outline-ring transition-colors hover:bg-muted focus-visible:outline-2 focus-visible:outline-offset-2"
          to="/back-office/users"
        >
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-md bg-muted">
              <UsersIcon className="size-5" />
            </div>
            <div className="min-w-0">
              <h2 className="text-base font-semibold">
                <Trans>Users</Trans>
              </h2>
              <p className="mt-1 text-sm text-muted-foreground">
                <Trans>Lookup users by name, email, or tenant.</Trans>
              </p>
            </div>
          </div>
        </RouterLink>
        <RouterLink
          className="rounded-md border border-border p-4 outline-ring transition-colors hover:bg-muted focus-visible:outline-2 focus-visible:outline-offset-2"
          to="/back-office/outbox"
        >
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-md bg-muted">
              <InboxIcon className="size-5" />
            </div>
            <div className="min-w-0">
              <h2 className="text-base font-semibold">
                <Trans>Outbox</Trans>
              </h2>
              <p className="mt-1 text-sm text-muted-foreground">
                <Trans>Monitor message delivery and retry failed work.</Trans>
              </p>
            </div>
          </div>
        </RouterLink>
      </div>
    </AppLayout>
  );
}

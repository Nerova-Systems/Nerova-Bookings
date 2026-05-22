import { Trans } from "@lingui/react/macro";
import { Link } from "@repo/ui/components/Link";
import { Section } from "@repo/ui/components/Section";

export function OrgBillingTab() {
  // TODO(u4-org-settings): A dedicated organization billing dashboard is not yet
  // ported. The existing BillingEndpoints/SubscriptionEndpoints are tenant-scoped
  // but render in the global Account > Billing page. A future task should bring
  // those views in here and add per-team usage breakdowns.
  return (
    <Section
      title={<Trans>Billing</Trans>}
      description={<Trans>Manage your organization's subscription and invoices.</Trans>}
    >
      <div className="flex flex-col gap-3">
        <p className="text-sm text-muted-foreground">
          <Trans>
            Organization-level billing controls are coming soon. In the meantime, you can manage your subscription from
            the main billing page.
          </Trans>
        </p>
        <div>
          <Link href="/account/billing">
            <Trans>Go to billing</Trans>
          </Link>
        </div>
      </div>
    </Section>
  );
}

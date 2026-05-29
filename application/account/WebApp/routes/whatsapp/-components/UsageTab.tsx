import { Trans } from "@lingui/react/macro";
import { BarChart3Icon, MessageSquareIcon } from "lucide-react";

export function UsageTab() {
  return (
    <div className="flex flex-col gap-6">
      {/* Coming soon header */}
      <div className="flex flex-col items-center gap-4 rounded-xl border border-dashed border-border bg-muted/20 py-12 text-center">
        <div className="flex size-14 items-center justify-center rounded-full bg-primary/10">
          <BarChart3Icon className="size-7 text-primary" />
        </div>
        <div>
          <h3 className="text-lg font-bold">
            <Trans>Usage tracking coming soon</Trans>
          </h3>
          <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
            <Trans>
              Full message usage analytics, cost breakdowns, and delivery reports are being built. In
              the meantime, here's what you need to know about WhatsApp messaging costs.
            </Trans>
          </p>
        </div>
      </div>

      {/* Cost breakdown */}
      <div>
        <h3 className="mb-3 text-sm font-bold uppercase tracking-wider text-muted-foreground">
          <Trans>Estimated message costs</Trans>
        </h3>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-xl border border-border bg-card p-5">
            <div className="mb-3 flex items-center gap-2">
              <div className="flex size-8 items-center justify-center rounded-lg bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-400">
                <MessageSquareIcon className="size-4" />
              </div>
              <h4 className="text-sm font-semibold">
                <Trans>Utility messages</Trans>
              </h4>
            </div>
            <p className="mb-2 text-xs text-muted-foreground">
              <Trans>Booking confirmations, reminders, status updates.</Trans>
            </p>
            <div className="rounded-lg bg-muted/50 px-3 py-2 text-center">
              <span className="text-lg font-bold">~R0.35</span>
              <span className="ml-1 text-xs text-muted-foreground">
                <Trans>per message (ZAR)</Trans>
              </span>
            </div>
          </div>

          <div className="rounded-xl border border-border bg-card p-5">
            <div className="mb-3 flex items-center gap-2">
              <div className="flex size-8 items-center justify-center rounded-lg bg-purple-100 text-purple-700 dark:bg-purple-950 dark:text-purple-400">
                <MessageSquareIcon className="size-4" />
              </div>
              <h4 className="text-sm font-semibold">
                <Trans>Marketing messages</Trans>
              </h4>
            </div>
            <p className="mb-2 text-xs text-muted-foreground">
              <Trans>Follow-ups, promotions, review requests. Coming soon.</Trans>
            </p>
            <div className="rounded-lg bg-muted/50 px-3 py-2 text-center">
              <span className="text-lg font-bold">~R0.80</span>
              <span className="ml-1 text-xs text-muted-foreground">
                <Trans>per message (ZAR)</Trans>
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Note */}
      <div className="rounded-lg border border-border bg-muted/30 p-4 text-xs text-muted-foreground">
        <Trans>
          Prices are approximate and based on Meta's current ZAR pricing. Actual costs may vary based on
          message category and Meta's pricing updates. Booking flow interactions within WhatsApp Flows do
          not incur per-message charges.
        </Trans>
      </div>
    </div>
  );
}

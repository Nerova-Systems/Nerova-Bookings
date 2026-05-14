import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { AlertTriangleIcon } from "lucide-react";

import type { SubscriptionPlan } from "@/shared/lib/api/client";

import { getPlanLabel } from "@/shared/lib/api/subscriptionPlan";

interface PaymentFailedBillingBannerProps {
  canRetryPayment: boolean;
  onRetryPayment: () => void;
  onUpdatePaymentMethod: () => void;
}

export function PaymentFailedBillingBanner({
  canRetryPayment,
  onRetryPayment,
  onUpdatePaymentMethod
}: Readonly<PaymentFailedBillingBannerProps>) {
  return (
    <div className="mb-6 flex flex-col items-start gap-3 rounded-lg border border-warning/50 bg-warning/15 p-4 text-sm text-warning-foreground sm:flex-row sm:items-center sm:justify-between">
      <div className="flex items-center gap-3">
        <AlertTriangleIcon className="size-4 shrink-0" />
        <Trans>Payment failed. Retry payment or update your payment method to keep your subscription active.</Trans>
      </div>
      <div className="flex w-full shrink-0 flex-col gap-2 sm:w-auto sm:flex-row">
        <Button size="sm" variant="secondary" className="w-full sm:w-auto" onClick={onUpdatePaymentMethod}>
          <Trans>Update payment method</Trans>
        </Button>
        <Button size="sm" className="w-full sm:w-auto" onClick={onRetryPayment} disabled={!canRetryPayment}>
          <Trans>Retry payment</Trans>
        </Button>
      </div>
    </div>
  );
}

interface CancellationBannerProps {
  currentPlan: SubscriptionPlan;
  formattedPeriodEnd: string | null;
  onReactivate?: () => void;
}

export function CancellationBanner({
  currentPlan,
  formattedPeriodEnd,
  onReactivate
}: Readonly<CancellationBannerProps>) {
  return (
    <div className="mb-6 flex items-center justify-between gap-3 rounded-lg border border-border bg-muted/50 p-4 text-sm text-muted-foreground">
      <div className="flex items-center gap-3">
        <AlertTriangleIcon className="size-4 shrink-0" />
        {formattedPeriodEnd ? (
          <Trans>
            Your {getPlanLabel(currentPlan)} subscription has been cancelled and will end on {formattedPeriodEnd}.
          </Trans>
        ) : (
          <Trans>Your subscription has been cancelled and will end at the end of the current billing period.</Trans>
        )}
      </div>
      {onReactivate && (
        <Button size="sm" className="shrink-0" onClick={onReactivate}>
          <Trans>Reactivate</Trans>
        </Button>
      )}
    </div>
  );
}

interface DowngradeBannerProps {
  scheduledPlan: SubscriptionPlan;
  formattedPeriodEnd: string | null;
  onCancelDowngrade?: () => void;
}

export function DowngradeBanner({
  scheduledPlan,
  formattedPeriodEnd,
  onCancelDowngrade
}: Readonly<DowngradeBannerProps>) {
  return (
    <div className="mb-6 flex items-center justify-between gap-3 rounded-lg border border-border bg-muted/50 p-4 text-sm text-muted-foreground">
      <div className="flex items-center gap-3">
        <AlertTriangleIcon className="size-4 shrink-0" />
        {formattedPeriodEnd ? (
          <Trans>
            Your subscription will be downgraded to {getPlanLabel(scheduledPlan)} on {formattedPeriodEnd}.
          </Trans>
        ) : (
          <Trans>
            Your subscription will be downgraded to {getPlanLabel(scheduledPlan)} at the end of the current billing
            period.
          </Trans>
        )}
      </div>
      {onCancelDowngrade && (
        <Button size="sm" className="shrink-0" onClick={onCancelDowngrade}>
          <Trans>Cancel downgrade</Trans>
        </Button>
      )}
    </div>
  );
}

export function PaystackNotConfiguredBanner() {
  return (
    <div className="mb-6 flex items-center gap-3 rounded-lg border border-border bg-muted/50 p-4 text-sm text-muted-foreground">
      <AlertTriangleIcon className="size-4 shrink-0" />
      <Trans>Billing is not configured. Please contact support to enable payment processing.</Trans>
    </div>
  );
}

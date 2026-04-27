import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

import { SubscriptionPlan } from "@/shared/lib/api/client";

import { getPlanOrder } from "./planUtils";

interface PlanActionProps {
  plan: SubscriptionPlan;
  currentPlan: SubscriptionPlan;
  cancelAtPeriodEnd: boolean;
  scheduledPlan: SubscriptionPlan | null;
  isPaymentConfigured: boolean;
  onSubscribe: (plan: SubscriptionPlan) => void;
  onUpgrade: (plan: SubscriptionPlan) => void;
  onDowngrade: (plan: SubscriptionPlan) => void;
  onReactivate: () => void;
  onCancelDowngrade: () => void;
  isPending: boolean;
  pendingPlan: SubscriptionPlan | null;
  isCancelDowngradePending: boolean;
}

export function PlanAction({
  plan,
  currentPlan,
  cancelAtPeriodEnd,
  scheduledPlan,
  isPaymentConfigured,
  onSubscribe,
  onUpgrade,
  onDowngrade,
  onReactivate,
  onCancelDowngrade,
  isPending,
  pendingPlan,
  isCancelDowngradePending
}: Readonly<PlanActionProps>) {
  const isCurrent = plan === currentPlan;
  const isScheduled = plan === scheduledPlan;
  const isThisPlanPending = pendingPlan === plan;
  const isUpgrade = getPlanOrder(plan) > getPlanOrder(currentPlan);
  const isDowngrade = getPlanOrder(plan) < getPlanOrder(currentPlan);
  const isBasis = currentPlan === SubscriptionPlan.Trial;
  const disabled = isPending || !isPaymentConfigured;

  if (cancelAtPeriodEnd) {
    if (!isCurrent) {
      return null;
    }
    return (
      <Button
        variant="default"
        className="w-full"
        onClick={onReactivate}
        isPending={isThisPlanPending}
        disabled={disabled}
      >
        {isThisPlanPending ? <Trans>Processing...</Trans> : <Trans>Reactivate</Trans>}
      </Button>
    );
  }
  if (isCurrent) {
    return (
      <Button variant="outline" className="w-full" disabled={true}>
        <Trans>Current plan</Trans>
      </Button>
    );
  }
  if (isBasis && plan !== SubscriptionPlan.Trial) {
    return (
      <Button
        variant="default"
        className="w-full"
        onClick={() => onSubscribe(plan)}
        isPending={isThisPlanPending}
        disabled={disabled}
      >
        {isThisPlanPending ? <Trans>Processing...</Trans> : <Trans>Subscribe</Trans>}
      </Button>
    );
  }
  if (isUpgrade) {
    return (
      <Button
        variant="default"
        className="w-full"
        onClick={() => onUpgrade(plan)}
        isPending={isThisPlanPending}
        disabled={disabled}
      >
        {isThisPlanPending ? <Trans>Processing...</Trans> : <Trans>Upgrade</Trans>}
      </Button>
    );
  }
  if (isDowngrade) {
    if (isScheduled) {
      return (
        <Button className="w-full" onClick={onCancelDowngrade} isPending={isCancelDowngradePending} disabled={disabled}>
          {isCancelDowngradePending ? <Trans>Processing...</Trans> : <Trans>Cancel downgrade</Trans>}
        </Button>
      );
    }
    return (
      <Button
        variant="secondary"
        className="w-full"
        onClick={() => onDowngrade(plan)}
        isPending={isThisPlanPending}
        disabled={disabled}
      >
        {isThisPlanPending ? <Trans>Processing...</Trans> : <Trans>Downgrade</Trans>}
      </Button>
    );
  }
  return null;
}

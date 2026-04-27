import { t } from "@lingui/core/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";

import type { components } from "@/shared/lib/api/api.generated";
import type { SubscriptionPlan } from "@/shared/lib/api/client";

import { SubscriptionPlan as Plans } from "@/shared/lib/api/client";

import { getFormattedPrice, PlanCard } from "./PlanCard";
import { BillingNotConfiguredBanner } from "./SubscriptionBanner";

type PlanPriceItem = components["schemas"]["PlanPriceItem"];

interface InitialPlanSelectionProps {
  plans: PlanPriceItem[] | undefined;
  currentPlan: SubscriptionPlan;
  isPaymentConfigured: boolean;
  onSubscribe: (plan: SubscriptionPlan) => void;
}

export function InitialPlanSelection({
  plans,
  currentPlan,
  isPaymentConfigured,
  onSubscribe
}: Readonly<InitialPlanSelectionProps>) {
  return (
    <AppLayout variant="center" maxWidth="64rem" title={t`Billing`} subtitle={t`Choose a plan to get started.`}>
      {!isPaymentConfigured && <BillingNotConfiguredBanner />}
      <div className="grid gap-4 lg:grid-cols-3">
        {[Plans.Starter, Plans.Standard, Plans.Premium].map((plan) => (
          <PlanCard
            key={plan}
            plan={plan}
            formattedPrice={getFormattedPrice(plan, plans)}
            currentPlan={currentPlan}
            cancelAtPeriodEnd={false}
            scheduledPlan={null}
            isPaymentConfigured={isPaymentConfigured}
            onSubscribe={onSubscribe}
            onUpgrade={() => {}}
            onDowngrade={() => {}}
            onReactivate={() => {}}
            onCancelDowngrade={() => {}}
            isPending={false}
            pendingPlan={null}
            isCancelDowngradePending={false}
          />
        ))}
      </div>
    </AppLayout>
  );
}

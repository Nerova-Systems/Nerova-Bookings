import type { components } from "@/shared/lib/api/api.generated";

import { type SubscriptionPlan, SubscriptionPlan as Plans } from "@/shared/lib/api/client";

import { getCatalogUnitAmount, getFormattedPrice, PlanCard } from "./PlanCard";
import { BillingNotConfiguredBanner } from "./SubscriptionBanner";

type PlanPriceItem = components["schemas"]["PlanPriceItem"];

interface PlanCardGridProps {
  plans: PlanPriceItem[] | undefined;
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
  currentPriceAmount: number | null | undefined;
  currentPriceCurrency: string | null | undefined;
}

export function PlanCardGrid({
  plans,
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
  isCancelDowngradePending,
  currentPriceAmount,
  currentPriceCurrency
}: Readonly<PlanCardGridProps>) {
  return (
    <>
      {!isPaymentConfigured && <BillingNotConfiguredBanner />}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[Plans.Trial, Plans.Starter, Plans.Standard, Plans.Premium].map((plan) => {
          const planItem = plans?.find((p) => p.plan === plan);
          const taxExclusive = planItem != null && !planItem.taxInclusive;
          return (
            <PlanCard
              key={plan}
              plan={plan}
              formattedPrice={getFormattedPrice(plan, plans)}
              currentPlan={currentPlan}
              cancelAtPeriodEnd={cancelAtPeriodEnd}
              scheduledPlan={scheduledPlan}
              isPaymentConfigured={isPaymentConfigured}
              onSubscribe={onSubscribe}
              onUpgrade={onUpgrade}
              onDowngrade={onDowngrade}
              onReactivate={onReactivate}
              onCancelDowngrade={onCancelDowngrade}
              isPending={isPending}
              pendingPlan={pendingPlan}
              isCancelDowngradePending={isCancelDowngradePending}
              currentPriceAmount={currentPriceAmount}
              currentPriceCurrency={currentPriceCurrency}
              catalogUnitAmount={getCatalogUnitAmount(plan, plans)}
              taxExclusive={taxExclusive}
            />
          );
        })}
      </div>
    </>
  );
}

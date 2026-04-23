import { t } from "@lingui/core/macro";
import { formatCurrency } from "@repo/utils/currency/formatCurrency";

import type { components } from "@/shared/lib/api/api.generated";

import { SubscriptionPlan } from "@/shared/lib/api/client";

type PlanPriceItem = components["schemas"]["PlanPriceItem"];

type PlanDetails = {
  name: string;
  features: string[];
};

export function getFormattedPrice(plan: SubscriptionPlan, pricingPlans: PlanPriceItem[] | undefined): string {
  const item = pricingPlans?.find((p) => p.plan === plan);
  if (item) {
    const price = formatCurrency(item.unitAmount, item.currency);
    return t`${price}/month`;
  }
  if (plan === SubscriptionPlan.Trial) {
    return t`Free`;
  }
  return "";
}

export function getCatalogUnitAmount(plan: SubscriptionPlan, pricingPlans: PlanPriceItem[] | undefined): number | null {
  return pricingPlans?.find((p) => p.plan === plan)?.unitAmount ?? null;
}

export function getPlanDetails(plan: SubscriptionPlan): PlanDetails {
  switch (plan) {
    case SubscriptionPlan.Trial:
      return {
        name: t`Trial`,
        features: [t`5 users`, t`10 GB storage`, t`Basic support`]
      };
    case SubscriptionPlan.Starter:
      return {
        name: t`Starter`,
        features: [t`5 users`, t`10 GB storage`, t`Email support`]
      };
    case SubscriptionPlan.Standard:
      return {
        name: t`Standard`,
        features: [t`10 users`, t`100 GB storage`, t`Email support`, t`Analytics`]
      };
    case SubscriptionPlan.Premium:
      return {
        name: t`Premium`,
        features: [t`Unlimited users`, t`1 TB storage`, t`Priority support`, t`Advanced analytics`, t`SLA`]
      };
  }
}

export function getPlanOrder(plan: SubscriptionPlan): number {
  switch (plan) {
    case SubscriptionPlan.Trial:
      return 0;
    case SubscriptionPlan.Starter:
      return 1;
    case SubscriptionPlan.Standard:
      return 2;
    case SubscriptionPlan.Premium:
      return 3;
  }
}

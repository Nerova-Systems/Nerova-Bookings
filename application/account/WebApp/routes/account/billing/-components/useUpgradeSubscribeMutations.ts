import { t } from "@lingui/core/macro";

import { api } from "@/shared/lib/api/client";

import type { useSubscriptionPolling } from "./useSubscriptionPolling";

interface UseUpgradeSubscribeMutationsOptions {
  startPolling: ReturnType<typeof useSubscriptionPolling>["startPolling"];
  setIsUpgradeDialogOpen: (open: boolean) => void;
  setIsSubscribeDialogOpen: (open: boolean) => void;
  setIsConfirmingPayment: (value: boolean) => void;
}

export function useUpgradeSubscribeMutations({
  startPolling,
  setIsUpgradeDialogOpen,
  setIsSubscribeDialogOpen,
  setIsConfirmingPayment
}: UseUpgradeSubscribeMutationsOptions) {
  const upgradeMutation = api.useMutation("post", "/api/account/subscriptions/upgrade", {
    onSuccess: async (data, variables) => {
      const targetPlan = variables.body?.newPlan;
      if (data.authorizationUrl) {
        setIsConfirmingPayment(true);
        window.location.assign(data.authorizationUrl);
        return;
      }

      startPolling({
        check: (subscription) => subscription.plan === targetPlan,
        successMessage: t`Your plan has been upgraded.`,
        onComplete: () => setIsUpgradeDialogOpen(false)
      });
    }
  });

  const subscribeMutation = api.useMutation("post", "/api/account/subscriptions/start-checkout", {
    onSuccess: async (data, variables) => {
      const targetPlan = variables.body?.plan;
      if (data.authorizationUrl) {
        setIsConfirmingPayment(true);
        window.location.assign(data.authorizationUrl);
        return;
      }

      startPolling({
        check: (sub) => sub.plan === targetPlan,
        successMessage: t`Your subscription has been activated.`,
        onComplete: () => setIsSubscribeDialogOpen(false)
      });
    }
  });

  return { upgradeMutation, subscribeMutation };
}

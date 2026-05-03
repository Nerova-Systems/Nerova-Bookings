import { t } from "@lingui/core/macro";

import { api } from "@/shared/lib/api/client";

import type { useSubscriptionPolling } from "./useSubscriptionPolling";

interface UseBillingPageMutationsOptions {
  startPolling: ReturnType<typeof useSubscriptionPolling>["startPolling"];
  setIsReactivateDialogOpen: (open: boolean) => void;
  setIsCancelDowngradeDialogOpen: (open: boolean) => void;
}

export function useBillingPageMutations({
  startPolling,
  setIsReactivateDialogOpen,
  setIsCancelDowngradeDialogOpen
}: UseBillingPageMutationsOptions) {
  const reactivateMutation = api.useMutation("post", "/api/account/subscriptions/reactivate", {
    onSuccess: () => {
      startPolling({
        check: (subscription) => subscription.cancelAtPeriodEnd === false,
        successMessage: t`Your subscription has been reactivated.`,
        onComplete: () => setIsReactivateDialogOpen(false)
      });
    }
  });

  const cancelDowngradeMutation = api.useMutation("post", "/api/account/subscriptions/cancel-downgrade", {
    onSuccess: () => {
      startPolling({
        check: (subscription) => subscription.scheduledPlan == null,
        successMessage: t`Your scheduled downgrade has been cancelled.`,
        onComplete: () => setIsCancelDowngradeDialogOpen(false)
      });
    }
  });

  return { reactivateMutation, cancelDowngradeMutation };
}

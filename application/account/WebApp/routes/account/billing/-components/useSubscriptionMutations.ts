import { t } from "@lingui/core/macro";

import { api } from "@/shared/lib/api/client";

import type { useSubscriptionPolling } from "./useSubscriptionPolling";

interface UseSubscriptionMutationsOptions {
  startPolling: ReturnType<typeof useSubscriptionPolling>["startPolling"];
  setIsCancelDialogOpen: (open: boolean) => void;
  setIsReactivateDialogOpen: (open: boolean) => void;
}

export function useSubscriptionLifecycleMutations({
  startPolling,
  setIsCancelDialogOpen,
  setIsReactivateDialogOpen
}: UseSubscriptionMutationsOptions) {
  const cancelMutation = api.useMutation("post", "/api/account/subscriptions/cancel", {
    onSuccess: () => {
      startPolling({
        check: (subscription) => subscription.cancelAtPeriodEnd === true,
        successMessage: t`Your subscription has been cancelled.`,
        onComplete: () => setIsCancelDialogOpen(false)
      });
    }
  });

  const reactivateMutation = api.useMutation("post", "/api/account/subscriptions/reactivate", {
    onSuccess: () => {
      startPolling({
        check: (subscription) => subscription.cancelAtPeriodEnd === false,
        successMessage: t`Your subscription has been reactivated.`,
        onComplete: () => setIsReactivateDialogOpen(false)
      });
    }
  });

  return { cancelMutation, reactivateMutation };
}

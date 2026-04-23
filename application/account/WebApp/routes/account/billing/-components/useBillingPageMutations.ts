import { t } from "@lingui/core/macro";
import { toast } from "sonner";

import { api, SubscriptionStatus } from "@/shared/lib/api/client";

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
    onSuccess: (data) => {
      if (data.uuid) {
        setIsReactivateDialogOpen(false);
        if (typeof window.payfast_do_onsite_payment === "function") {
          window.payfast_do_onsite_payment({ uuid: data.uuid });
        } else {
          toast.error(t`Payment processor unavailable. Please refresh and try again.`);
        }
      } else {
        startPolling({
          check: (subscription) => subscription.status === SubscriptionStatus.Active,
          successMessage: t`Your subscription has been reactivated.`,
          onComplete: () => setIsReactivateDialogOpen(false)
        });
      }
    }
  });

  const cancelDowngradeMutation = api.useMutation("post", "/api/account/subscriptions/cancel-scheduled-downgrade", {
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

import { t } from "@lingui/core/macro";
import { toast } from "sonner";

import { api, type SubscriptionPlan, SubscriptionStatus } from "@/shared/lib/api/client";

import type { useSubscriptionPolling } from "./useSubscriptionPolling";

interface UseSubscriptionMutationsOptions {
  startPolling: ReturnType<typeof useSubscriptionPolling>["startPolling"];
  currentPlan: SubscriptionPlan;
  downgradeTarget: SubscriptionPlan;
  setIsDowngradeDialogOpen: (open: boolean) => void;
  setIsCancelDialogOpen: (open: boolean) => void;
  setIsCancelDowngradeDialogOpen: (open: boolean) => void;
  setIsReactivateDialogOpen: (open: boolean) => void;
}

export function useSubscriptionLifecycleMutations({
  startPolling,
  downgradeTarget,
  setIsDowngradeDialogOpen,
  setIsCancelDialogOpen,
  setIsCancelDowngradeDialogOpen,
  setIsReactivateDialogOpen
}: UseSubscriptionMutationsOptions) {
  const downgradeMutation = api.useMutation("post", "/api/account/subscriptions/schedule-downgrade", {
    onSuccess: () => {
      startPolling({
        check: (subscription) => subscription.scheduledPlan === downgradeTarget,
        successMessage: t`Your downgrade has been scheduled.`,
        onComplete: () => setIsDowngradeDialogOpen(false)
      });
    }
  });

  const cancelMutation = api.useMutation("post", "/api/account/subscriptions/cancel", {
    onSuccess: () => {
      startPolling({
        check: (subscription) => subscription.status === SubscriptionStatus.Cancelled,
        successMessage: t`Your subscription has been cancelled.`,
        onComplete: () => setIsCancelDialogOpen(false)
      });
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

  return { downgradeMutation, cancelMutation, cancelDowngradeMutation, reactivateMutation };
}

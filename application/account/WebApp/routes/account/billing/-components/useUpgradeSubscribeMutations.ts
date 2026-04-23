import { t } from "@lingui/core/macro";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import type { useSubscriptionPolling } from "./useSubscriptionPolling";

interface UseUpgradeSubscribeMutationsOptions {
  startPolling: ReturnType<typeof useSubscriptionPolling>["startPolling"];
  setIsUpgradeDialogOpen: (open: boolean) => void;
  setIsSubscribeDialogOpen: (open: boolean) => void;
}

export function useUpgradeSubscribeMutations({
  startPolling,
  setIsUpgradeDialogOpen,
  setIsSubscribeDialogOpen
}: UseUpgradeSubscribeMutationsOptions) {
  const upgradeMutation = api.useMutation("post", "/api/account/subscriptions/upgrade", {
    onSuccess: (_, variables) => {
      const targetPlan = variables.body?.newPlan;
      startPolling({
        check: (subscription) => subscription.plan === targetPlan,
        successMessage: t`Your plan has been upgraded.`,
        onComplete: () => setIsUpgradeDialogOpen(false)
      });
    }
  });

  const subscribeMutation = api.useMutation("post", "/api/account/subscriptions/initiate", {
    onSuccess: (data) => {
      if (typeof window.payfast_do_onsite_payment === "function") {
        window.payfast_do_onsite_payment({ uuid: data.uuid });
      } else {
        toast.error(t`Payment processor unavailable. Please refresh and try again.`);
      }
      setIsSubscribeDialogOpen(false);
    }
  });

  return { upgradeMutation, subscribeMutation };
}

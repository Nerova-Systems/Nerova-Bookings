import { t } from "@lingui/core/macro";

import { api, PaystackPaymentPurpose } from "@/shared/lib/api/client";

import type { useSubscriptionPolling } from "./useSubscriptionPolling";

interface UseUpgradeSubscribeMutationsOptions {
  startPolling: ReturnType<typeof useSubscriptionPolling>["startPolling"];
  setIsUpgradeDialogOpen: (open: boolean) => void;
  setIsSubscribeDialogOpen: (open: boolean) => void;
  setIsConfirmingPayment: (value: boolean) => void;
}

type PaystackPaymentResponse = {
  reference?: string | null;
  operationPurpose?: PaystackPaymentPurpose | null;
};

export function useUpgradeSubscribeMutations({
  startPolling,
  setIsUpgradeDialogOpen,
  setIsSubscribeDialogOpen,
  setIsConfirmingPayment
}: UseUpgradeSubscribeMutationsOptions) {
  const confirmPaymentMutation = api.useMutation("post", "/api/account/subscriptions/confirm-payment");

  const upgradeMutation = api.useMutation("post", "/api/account/subscriptions/upgrade", {
    onSuccess: async (data, variables) => {
      const targetPlan = variables.body?.newPlan;
      const payment = data as PaystackPaymentResponse;
      if (payment.reference && targetPlan) {
        setIsConfirmingPayment(true);
        try {
          await confirmPaymentMutation.mutateAsync({
            body: {
              reference: payment.reference,
              plan: targetPlan,
              purpose: payment.operationPurpose ?? PaystackPaymentPurpose.Upgrade
            }
          });
        } finally {
          setIsConfirmingPayment(false);
        }
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
      const payment = data as PaystackPaymentResponse;
      if (payment.reference && targetPlan) {
        setIsConfirmingPayment(true);
        try {
          await confirmPaymentMutation.mutateAsync({
            body: {
              reference: payment.reference,
              plan: targetPlan,
              purpose: payment.operationPurpose ?? PaystackPaymentPurpose.Subscribe
            }
          });
        } finally {
          setIsConfirmingPayment(false);
        }
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

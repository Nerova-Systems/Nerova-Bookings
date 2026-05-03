import { useState } from "react";

import { api, SubscriptionPlan } from "@/shared/lib/api/client";

import { useSubscriptionLifecycleMutations } from "./useSubscriptionMutations";
import { useSubscriptionPolling } from "./useSubscriptionPolling";
import { useUpgradeSubscribeMutations } from "./useUpgradeSubscribeMutations";

export function usePlansPageState() {
  const { isPolling, startPolling, subscription } = useSubscriptionPolling();
  const [isUpgradeDialogOpen, setIsUpgradeDialogOpen] = useState(false);
  const [upgradeTarget, setUpgradeTarget] = useState<SubscriptionPlan>(SubscriptionPlan.Standard);
  const [isCancelDialogOpen, setIsCancelDialogOpen] = useState(false);
  const [isReactivateDialogOpen, setIsReactivateDialogOpen] = useState(false);
  const [isCheckoutDialogOpen, setIsCheckoutDialogOpen] = useState(false);
  const [isEditBillingInfoOpen, setIsEditBillingInfoOpen] = useState(false);
  const [checkoutPlan, setCheckoutPlan] = useState<SubscriptionPlan>(SubscriptionPlan.Standard);
  const [pendingCheckoutPlan, setPendingCheckoutPlan] = useState<SubscriptionPlan | null>(null);
  const [isConfirmingPayment, setIsConfirmingPayment] = useState(false);
  const [isSubscribeDialogOpen, setIsSubscribeDialogOpen] = useState(false);
  const [subscribeTarget, setSubscribeTarget] = useState<SubscriptionPlan>(SubscriptionPlan.Standard);

  const { data: tenant } = api.useQuery("get", "/api/account/tenants/current");
  const { data: pricingCatalog } = api.useQuery("get", "/api/account/subscriptions/pricing-catalog");
  const currentPlan = subscription?.plan ?? SubscriptionPlan.Basis;

  const { upgradeMutation, subscribeMutation } = useUpgradeSubscribeMutations({
    startPolling,
    setIsUpgradeDialogOpen,
    setIsSubscribeDialogOpen,
    setIsConfirmingPayment
  });

  const { cancelMutation, reactivateMutation } = useSubscriptionLifecycleMutations({
    startPolling,
    setIsCancelDialogOpen,
    setIsReactivateDialogOpen
  });

  const isPending =
    upgradeMutation.isPending ||
    subscribeMutation.isPending ||
    reactivateMutation.isPending ||
    cancelMutation.isPending ||
    isPolling;

  const pendingPlan = upgradeMutation.isPending
    ? (upgradeMutation.variables?.body?.newPlan ?? null)
    : cancelMutation.isPending
      ? SubscriptionPlan.Basis
      : reactivateMutation.isPending
        ? currentPlan
        : null;

  const handleBillingInfoSuccess = () => {
    if (pendingCheckoutPlan == null) return;
    setCheckoutPlan(pendingCheckoutPlan);
    setPendingCheckoutPlan(null);
    setIsCheckoutDialogOpen(true);
  };

  return {
    subscription,
    isPolling,
    isConfirmingPayment,
    tenant,
    pricingCatalog,
    currentPlan,
    isPending,
    pendingPlan,
    isUpgradeDialogOpen,
    setIsUpgradeDialogOpen,
    upgradeTarget,
    setUpgradeTarget,
    isCancelDialogOpen,
    setIsCancelDialogOpen,
    isReactivateDialogOpen,
    setIsReactivateDialogOpen,
    isCheckoutDialogOpen,
    setIsCheckoutDialogOpen,
    isEditBillingInfoOpen,
    setIsEditBillingInfoOpen,
    checkoutPlan,
    pendingCheckoutPlan,
    setPendingCheckoutPlan,
    isSubscribeDialogOpen,
    setIsSubscribeDialogOpen,
    subscribeTarget,
    setSubscribeTarget,
    upgradeMutation,
    subscribeMutation,
    cancelMutation,
    reactivateMutation,
    handleBillingInfoSuccess
  };
}

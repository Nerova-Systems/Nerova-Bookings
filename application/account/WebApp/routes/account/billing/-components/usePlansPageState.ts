import { useState } from "react";

import { api, SubscriptionPlan } from "@/shared/lib/api/client";

import { useSubscriptionLifecycleMutations } from "./useSubscriptionMutations";
import { useSubscriptionPolling } from "./useSubscriptionPolling";
import { useUpgradeSubscribeMutations } from "./useUpgradeSubscribeMutations";

export function usePlansPageState() {
  const { isPolling, startPolling, subscription } = useSubscriptionPolling();
  const [isUpgradeDialogOpen, setIsUpgradeDialogOpen] = useState(false);
  const [upgradeTarget, setUpgradeTarget] = useState<SubscriptionPlan>(SubscriptionPlan.Standard);
  const [isDowngradeDialogOpen, setIsDowngradeDialogOpen] = useState(false);
  const [downgradeTarget, setDowngradeTarget] = useState<SubscriptionPlan>(SubscriptionPlan.Standard);
  const [isCancelDialogOpen, setIsCancelDialogOpen] = useState(false);
  const [isCancelDowngradeDialogOpen, setIsCancelDowngradeDialogOpen] = useState(false);
  const [isReactivateDialogOpen, setIsReactivateDialogOpen] = useState(false);
  const [isSubscribeDialogOpen, setIsSubscribeDialogOpen] = useState(false);
  const [subscribeTarget, setSubscribeTarget] = useState<SubscriptionPlan>(SubscriptionPlan.Starter);

  const { data: tenant } = api.useQuery("get", "/api/account/tenants/current");
  const { data: pricingCatalog } = api.useQuery("get", "/api/account/subscriptions/pricing-catalog");
  const currentPlan = subscription?.plan ?? SubscriptionPlan.Trial;

  const { upgradeMutation, subscribeMutation } = useUpgradeSubscribeMutations({
    startPolling,
    setIsUpgradeDialogOpen,
    setIsSubscribeDialogOpen
  });

  const { downgradeMutation, cancelMutation, cancelDowngradeMutation, reactivateMutation } =
    useSubscriptionLifecycleMutations({
      startPolling,
      currentPlan,
      downgradeTarget,
      setIsDowngradeDialogOpen,
      setIsCancelDialogOpen,
      setIsCancelDowngradeDialogOpen,
      setIsReactivateDialogOpen
    });

  const isPending =
    upgradeMutation.isPending ||
    subscribeMutation.isPending ||
    downgradeMutation.isPending ||
    cancelDowngradeMutation.isPending ||
    reactivateMutation.isPending ||
    cancelMutation.isPending ||
    isPolling;

  const pendingPlan = upgradeMutation.isPending
    ? (upgradeMutation.variables?.body?.newPlan ?? null)
    : downgradeMutation.isPending
      ? downgradeTarget
      : cancelMutation.isPending
        ? SubscriptionPlan.Trial
        : reactivateMutation.isPending
          ? currentPlan
          : null;

  return {
    subscription,
    isPolling,
    tenant,
    pricingCatalog,
    currentPlan,
    isPending,
    pendingPlan,
    isUpgradeDialogOpen,
    setIsUpgradeDialogOpen,
    upgradeTarget,
    setUpgradeTarget,
    isDowngradeDialogOpen,
    setIsDowngradeDialogOpen,
    downgradeTarget,
    setDowngradeTarget,
    isCancelDialogOpen,
    setIsCancelDialogOpen,
    isCancelDowngradeDialogOpen,
    setIsCancelDowngradeDialogOpen,
    isReactivateDialogOpen,
    setIsReactivateDialogOpen,
    isSubscribeDialogOpen,
    setIsSubscribeDialogOpen,
    subscribeTarget,
    setSubscribeTarget,
    upgradeMutation,
    subscribeMutation,
    downgradeMutation,
    cancelMutation,
    cancelDowngradeMutation,
    reactivateMutation
  };
}

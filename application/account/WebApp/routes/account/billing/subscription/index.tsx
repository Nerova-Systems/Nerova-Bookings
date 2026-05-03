import { t } from "@lingui/core/macro";
import { requirePermission, requireSubscriptionEnabled } from "@repo/infrastructure/auth/routeGuards";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { useFormatLongDate } from "@repo/ui/hooks/useSmartDate";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useRef } from "react";
import { toast } from "sonner";

import { api, SubscriptionPlan } from "@/shared/lib/api/client";

import { BillingTabNavigation } from "../-components/BillingTabNavigation";
import { PlanCardGrid } from "../-components/PlanCardGrid";
import { CancellationBanner, PaystackNotConfiguredBanner } from "../-components/SubscriptionBanner";
import { SubscriptionDialogs } from "../-components/SubscriptionDialogs";
import { usePlansPageState } from "../-components/usePlansPageState";

export const Route = createFileRoute("/account/billing/subscription/")({
  staticData: { trackingTitle: "Subscription" },
  beforeLoad: () => {
    requireSubscriptionEnabled();
    requirePermission({ allowedRoles: ["Owner"] });
  },
  component: PlansPage
});

function PlansPage() {
  const state = usePlansPageState();
  const formatLongDate = useFormatLongDate();
  const queryClient = useQueryClient();
  const handledCheckoutReference = useRef<string | null>(null);

  const cancelAtPeriodEnd = state.subscription?.cancelAtPeriodEnd ?? false;
  const currentPeriodEnd = state.subscription?.currentPeriodEnd ?? null;
  const formattedPeriodEnd = formatLongDate(currentPeriodEnd);
  const isPaystackConfigured = (state.pricingCatalog?.plans?.length ?? 0) > 0;
  const confirmCheckoutMutation = api.useMutation("post", "/api/account/subscriptions/confirm-checkout", {
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["get", "/api/account/subscriptions/current"] });
      toast.success(t`Your subscription has been activated.`);
    },
    onError: () => {
      toast.error(t`Payment could not be verified yet.`);
    }
  });
  const confirmCheckout = confirmCheckoutMutation.mutate;

  useEffect(() => {
    const searchParams = new URLSearchParams(window.location.search);
    const reference = searchParams.get("reference") ?? searchParams.get("trxref");
    if (!reference || handledCheckoutReference.current === reference) {
      return;
    }

    handledCheckoutReference.current = reference;
    window.history.replaceState(null, "", window.location.pathname);
    confirmCheckout({ body: { reference } });
  }, [confirmCheckout]);

  const handleSubscribe = (plan: SubscriptionPlan) => {
    if (state.subscription?.billingInfo && state.subscription?.paymentMethod) {
      state.setSubscribeTarget(plan);
      state.setIsSubscribeDialogOpen(true);
    } else {
      state.setPendingCheckoutPlan(plan);
      state.setIsEditBillingInfoOpen(true);
    }
  };

  const handleDowngrade = (plan: SubscriptionPlan) => {
    if (plan === SubscriptionPlan.Basis) {
      state.setIsCancelDialogOpen(true);
    }
  };

  return (
    <>
      <AppLayout variant="center" maxWidth="64rem" title={t`Subscription`} subtitle={t`Manage your subscription plan.`}>
        <BillingTabNavigation activeTab="subscription" />
        {cancelAtPeriodEnd && (
          <CancellationBanner currentPlan={state.currentPlan} formattedPeriodEnd={formattedPeriodEnd} />
        )}
        {!isPaystackConfigured && <PaystackNotConfiguredBanner />}
        <PlanCardGrid
          plans={state.pricingCatalog?.plans}
          currentPlan={state.currentPlan}
          cancelAtPeriodEnd={cancelAtPeriodEnd}
          isPaystackConfigured={isPaystackConfigured}
          onSubscribe={handleSubscribe}
          onUpgrade={(plan) => {
            state.setUpgradeTarget(plan);
            state.setIsUpgradeDialogOpen(true);
          }}
          onDowngrade={handleDowngrade}
          onReactivate={() => state.setIsReactivateDialogOpen(true)}
          isPending={state.isPending}
          pendingPlan={state.pendingPlan}
          currentPriceAmount={state.subscription?.currentPriceAmount}
          currentPriceCurrency={state.subscription?.currentPriceCurrency}
        />
      </AppLayout>
      <SubscriptionDialogs
        isCancelDialogOpen={state.isCancelDialogOpen}
        setIsCancelDialogOpen={state.setIsCancelDialogOpen}
        onCancelConfirm={(reason, feedback) => state.cancelMutation.mutate({ body: { reason, feedback } })}
        isCancelPending={state.cancelMutation.isPending || state.isPolling}
        currentPeriodEnd={currentPeriodEnd}
        isUpgradeDialogOpen={state.isUpgradeDialogOpen}
        setIsUpgradeDialogOpen={state.setIsUpgradeDialogOpen}
        onUpgradeConfirm={() => state.upgradeMutation.mutate({ body: { newPlan: state.upgradeTarget } })}
        isUpgradePending={state.upgradeMutation.isPending || state.isConfirmingPayment || state.isPolling}
        upgradeTarget={state.upgradeTarget}
        isSubscribeDialogOpen={state.isSubscribeDialogOpen}
        setIsSubscribeDialogOpen={state.setIsSubscribeDialogOpen}
        onSubscribeConfirm={() => state.subscribeMutation.mutate({ body: { plan: state.subscribeTarget } })}
        isSubscribePending={state.subscribeMutation.isPending || state.isConfirmingPayment || state.isPolling}
        subscribeTarget={state.subscribeTarget}
        currentPlan={state.currentPlan}
        isReactivateDialogOpen={state.isReactivateDialogOpen}
        setIsReactivateDialogOpen={state.setIsReactivateDialogOpen}
        onReactivateConfirm={() => state.reactivateMutation.mutate({})}
        isReactivatePending={state.reactivateMutation.isPending || state.isPolling}
        isEditBillingInfoOpen={state.isEditBillingInfoOpen}
        setIsEditBillingInfoOpen={state.setIsEditBillingInfoOpen}
        billingInfo={state.subscription?.billingInfo}
        paymentMethod={state.subscription?.paymentMethod}
        tenantName={state.tenant?.name ?? ""}
        onBillingInfoSuccess={state.handleBillingInfoSuccess}
        isCheckoutDialogOpen={state.isCheckoutDialogOpen}
        setIsCheckoutDialogOpen={state.setIsCheckoutDialogOpen}
        checkoutPlan={state.checkoutPlan}
      />
    </>
  );
}

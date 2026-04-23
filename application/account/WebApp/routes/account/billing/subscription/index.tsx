import { t } from "@lingui/core/macro";
import { requirePermission, requireSubscriptionEnabled } from "@repo/infrastructure/auth/routeGuards";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { useFormatLongDate } from "@repo/ui/hooks/useSmartDate";
import { createFileRoute } from "@tanstack/react-router";

import { SubscriptionPlan, SubscriptionStatus } from "@/shared/lib/api/client";

import { BillingTabNavigation } from "../-components/BillingTabNavigation";
import { PlanCardGrid } from "../-components/PlanCardGrid";
import { CancellationBanner, DowngradeBanner } from "../-components/SubscriptionBanner";
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

  const isCancelled = state.subscription?.status === SubscriptionStatus.Cancelled;
  const scheduledPlan = state.subscription?.scheduledPlan ?? null;
  const currentPeriodEnd = state.subscription?.currentPeriodEnd ?? null;
  const formattedPeriodEnd = formatLongDate(currentPeriodEnd);

  const handleSubscribe = (plan: SubscriptionPlan) => {
    state.setSubscribeTarget(plan);
    state.setIsSubscribeDialogOpen(true);
  };

  const handleDowngrade = (plan: SubscriptionPlan) => {
    if (plan === SubscriptionPlan.Trial) {
      state.setIsCancelDialogOpen(true);
    } else {
      state.setDowngradeTarget(plan);
      state.setIsDowngradeDialogOpen(true);
    }
  };

  return (
    <>
      <AppLayout variant="center" maxWidth="64rem" title={t`Subscription`} subtitle={t`Manage your subscription plan.`}>
        <BillingTabNavigation activeTab="subscription" />
        {isCancelled && (
          <CancellationBanner currentPlan={state.currentPlan} formattedPeriodEnd={formattedPeriodEnd} />
        )}
        {scheduledPlan && !isCancelled && (
          <DowngradeBanner scheduledPlan={scheduledPlan} formattedPeriodEnd={formattedPeriodEnd} />
        )}
        <PlanCardGrid
          plans={state.pricingCatalog?.plans}
          currentPlan={state.currentPlan}
          cancelAtPeriodEnd={isCancelled}
          scheduledPlan={scheduledPlan}
          isStripeConfigured={true}
          onSubscribe={handleSubscribe}
          onUpgrade={(plan) => {
            state.setUpgradeTarget(plan);
            state.setIsUpgradeDialogOpen(true);
          }}
          onDowngrade={handleDowngrade}
          onReactivate={() => state.setIsReactivateDialogOpen(true)}
          onCancelDowngrade={() => state.setIsCancelDowngradeDialogOpen(true)}
          isPending={state.isPending}
          pendingPlan={state.pendingPlan}
          isCancelDowngradePending={state.cancelDowngradeMutation.isPending}
          currentPriceAmount={undefined}
          currentPriceCurrency={undefined}
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
        isUpgradePending={state.upgradeMutation.isPending || state.isPolling}
        upgradeTarget={state.upgradeTarget}
        isSubscribeDialogOpen={state.isSubscribeDialogOpen}
        setIsSubscribeDialogOpen={state.setIsSubscribeDialogOpen}
        onSubscribeConfirm={() => state.subscribeMutation.mutate({ body: { plan: state.subscribeTarget } })}
        isSubscribePending={state.subscribeMutation.isPending || state.isPolling}
        subscribeTarget={state.subscribeTarget}
        isDowngradeDialogOpen={state.isDowngradeDialogOpen}
        setIsDowngradeDialogOpen={state.setIsDowngradeDialogOpen}
        onDowngradeConfirm={() => state.downgradeMutation.mutate({ body: { newPlan: state.downgradeTarget } })}
        isDowngradePending={state.downgradeMutation.isPending || state.isPolling}
        downgradeTarget={state.downgradeTarget}
        scheduledPlan={scheduledPlan}
        isCancelDowngradeDialogOpen={state.isCancelDowngradeDialogOpen}
        setIsCancelDowngradeDialogOpen={state.setIsCancelDowngradeDialogOpen}
        onCancelDowngradeConfirm={() => state.cancelDowngradeMutation.mutate({})}
        isCancelDowngradePending={state.cancelDowngradeMutation.isPending || state.isPolling}
        currentPlan={state.currentPlan}
        isReactivateDialogOpen={state.isReactivateDialogOpen}
        setIsReactivateDialogOpen={state.setIsReactivateDialogOpen}
        onReactivateConfirm={() => state.reactivateMutation.mutate({})}
        isReactivatePending={state.reactivateMutation.isPending || state.isPolling}
      />
    </>
  );
}

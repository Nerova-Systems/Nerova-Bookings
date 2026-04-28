import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { requirePermission, requireSubscriptionEnabled } from "@repo/infrastructure/auth/routeGuards";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Separator } from "@repo/ui/components/Separator";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { useFormatLongDate } from "@repo/ui/hooks/useSmartDate";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";

import { api, SubscriptionPlan, SubscriptionStatus } from "@/shared/lib/api/client";

import { BillingHistoryTable } from "./-components/BillingHistoryTable";
import { BillingTabNavigation } from "./-components/BillingTabNavigation";
import { CancelDowngradeDialog } from "./-components/CancelDowngradeDialog";
import { CurrentPlanSection } from "./-components/CurrentPlanSection";
import { InitialPlanSelection } from "./-components/InitialPlanSelection";
import { ReactivateConfirmationDialog } from "./-components/ReactivateConfirmationDialog";
import { RetryPaymentDialog } from "./-components/RetryPaymentDialog";
import { SubscribeConfirmationDialog } from "./-components/SubscribeConfirmationDialog";
import { CancellationBanner, DowngradeBanner, PastDueBanner } from "./-components/SubscriptionBanner";
import { useBillingPageMutations } from "./-components/useBillingPageMutations";
import { useSubscriptionPolling } from "./-components/useSubscriptionPolling";
import { useUpgradeSubscribeMutations } from "./-components/useUpgradeSubscribeMutations";

export const Route = createFileRoute("/account/billing/")({
  staticData: { trackingTitle: "Billing" },
  beforeLoad: () => {
    requireSubscriptionEnabled();
    requirePermission({ allowedRoles: ["Owner"] });
  },
  component: BillingPage
});

function BillingPage() {
  const formatLongDate = useFormatLongDate();
  const { isPolling, isLoading, startPolling, subscription } = useSubscriptionPolling();
  const [isCancelDowngradeDialogOpen, setIsCancelDowngradeDialogOpen] = useState(false);
  const [isReactivateDialogOpen, setIsReactivateDialogOpen] = useState(false);
  const [isSubscribeDialogOpen, setIsSubscribeDialogOpen] = useState(false);
  const [isRetryPaymentDialogOpen, setIsRetryPaymentDialogOpen] = useState(false);
  const [subscribeTarget, setSubscribeTarget] = useState<SubscriptionPlan>(SubscriptionPlan.Starter);

  const { data: pricingCatalog } = api.useQuery("get", "/api/account/subscriptions/pricing-catalog");
  const currentPlan = subscription?.plan ?? SubscriptionPlan.Trial;
  const isPaymentConfigured = pricingCatalog == null || pricingCatalog.plans.length > 0;

  const { reactivateMutation, cancelDowngradeMutation } = useBillingPageMutations({
    startPolling,
    setIsReactivateDialogOpen,
    setIsCancelDowngradeDialogOpen
  });
  const { subscribeMutation } = useUpgradeSubscribeMutations({
    startPolling,
    setIsUpgradeDialogOpen: () => {},
    setIsSubscribeDialogOpen
  });

  const isCancelled = subscription?.status === SubscriptionStatus.Cancelled;
  const isPastDue = subscription?.status === SubscriptionStatus.PastDue;
  const isSubscribed = subscription?.status !== SubscriptionStatus.Trial;
  const scheduledPlan = subscription?.scheduledPlan ?? null;
  const currentPeriodEnd = subscription?.currentPeriodEnd ?? null;
  const formattedPeriodEndLong = formatLongDate(currentPeriodEnd);
  const formattedGracePeriodEnd = formatLongDate(subscription?.gracePeriodEndsAt ?? null);
  const currentPlanPrice = pricingCatalog?.plans.find((plan) => plan.plan === currentPlan);

  if (isLoading) {
    return (
      <AppLayout variant="center" maxWidth="64rem" title={t`Billing`}>
        <Skeleton className="h-6 w-48" />
      </AppLayout>
    );
  }

  return (
    <>
      {isSubscribed ? (
        <AppLayout
          variant="center"
          maxWidth="64rem"
          title={t`Billing`}
          subtitle={t`Manage your subscription and billing history.`}
        >
          <BillingTabNavigation activeTab="billing" />
          {isCancelled && (
            <CancellationBanner
              currentPlan={currentPlan}
              formattedPeriodEnd={formattedPeriodEndLong}
              onReactivate={() => setIsReactivateDialogOpen(true)}
            />
          )}
          {isPastDue && (
            <PastDueBanner
              formattedGracePeriodEnd={formattedGracePeriodEnd}
              onRetryPayment={() => setIsRetryPaymentDialogOpen(true)}
            />
          )}
          {scheduledPlan && !isCancelled && (
            <DowngradeBanner
              scheduledPlan={scheduledPlan}
              formattedPeriodEnd={formattedPeriodEndLong}
              onCancelDowngrade={() => setIsCancelDowngradeDialogOpen(true)}
            />
          )}
          <CurrentPlanSection
            currentPlan={currentPlan}
            cancelAtPeriodEnd={isCancelled}
            scheduledPlan={scheduledPlan}
            formattedPeriodEndLong={formattedPeriodEndLong}
            currentPriceAmount={undefined}
            currentPriceCurrency={undefined}
            plans={pricingCatalog?.plans}
          />
          <div className="mt-8 flex flex-col gap-4">
            <h3>
              <Trans>Billing history</Trans>
            </h3>
            <Separator />
            <BillingHistoryTable />
          </div>
        </AppLayout>
      ) : (
        <InitialPlanSelection
          plans={pricingCatalog?.plans}
          currentPlan={currentPlan}
          isPaymentConfigured={isPaymentConfigured}
          onSubscribe={(plan) => {
            setSubscribeTarget(plan);
            setIsSubscribeDialogOpen(true);
          }}
        />
      )}

      <ReactivateConfirmationDialog
        isOpen={isReactivateDialogOpen}
        onOpenChange={setIsReactivateDialogOpen}
        onConfirm={() => reactivateMutation.mutate({})}
        isPending={reactivateMutation.isPending || isPolling}
        currentPlan={currentPlan}
      />

      {scheduledPlan && (
        <CancelDowngradeDialog
          isOpen={isCancelDowngradeDialogOpen}
          onOpenChange={setIsCancelDowngradeDialogOpen}
          onConfirm={() => cancelDowngradeMutation.mutate({})}
          isPending={cancelDowngradeMutation.isPending || isPolling}
          currentPlan={currentPlan}
          scheduledPlan={scheduledPlan}
          currentPeriodEnd={currentPeriodEnd}
        />
      )}

      <SubscribeConfirmationDialog
        isOpen={isSubscribeDialogOpen}
        onOpenChange={setIsSubscribeDialogOpen}
        onConfirm={() => subscribeMutation.mutate({ body: { plan: subscribeTarget } })}
        isPending={subscribeMutation.isPending || isPolling}
        targetPlan={subscribeTarget}
        billingInfo={subscription?.billingInfo}
        paymentMethod={subscription?.paymentMethod}
      />

      <RetryPaymentDialog
        isOpen={isRetryPaymentDialogOpen}
        onOpenChange={setIsRetryPaymentDialogOpen}
        billingInfo={subscription?.billingInfo}
        paymentMethod={subscription?.paymentMethod}
        amount={currentPlanPrice?.unitAmount ?? 0}
        currency={currentPlanPrice?.currency ?? "ZAR"}
      />
    </>
  );
}

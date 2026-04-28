import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { requirePermission, requireSubscriptionEnabled } from "@repo/infrastructure/auth/routeGuards";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Separator } from "@repo/ui/components/Separator";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { useFormatLongDate } from "@repo/ui/hooks/useSmartDate";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { api, SubscriptionPlan, SubscriptionStatus } from "@/shared/lib/api/client";

import { BillingHistoryTable } from "./-components/BillingHistoryTable";
import { BillingTabNavigation } from "./-components/BillingTabNavigation";
import { CancelDowngradeDialog } from "./-components/CancelDowngradeDialog";
import { CurrentPlanSection } from "./-components/CurrentPlanSection";
import { InitialPlanSelection } from "./-components/InitialPlanSelection";
import { ReactivateConfirmationDialog } from "./-components/ReactivateConfirmationDialog";
import { CancellationBanner, DowngradeBanner, PastDueBanner } from "./-components/SubscriptionBanner";
import { useBillingPageMutations } from "./-components/useBillingPageMutations";
import { useSubscriptionPolling } from "./-components/useSubscriptionPolling";

export const Route = createFileRoute("/account/billing/")({
  staticData: { trackingTitle: "Billing" },
  beforeLoad: () => {
    requireSubscriptionEnabled();
    requirePermission({ allowedRoles: ["Owner"] });
  },
  component: BillingPage
});

function BillingPage() {
  const navigate = useNavigate();
  const formatLongDate = useFormatLongDate();
  const { isPolling, isLoading, startPolling, subscription } = useSubscriptionPolling();
  const [isCancelDowngradeDialogOpen, setIsCancelDowngradeDialogOpen] = useState(false);
  const [isReactivateDialogOpen, setIsReactivateDialogOpen] = useState(false);

  const { data: pricingCatalog } = api.useQuery("get", "/api/account/subscriptions/pricing-catalog");
  const currentPlan = subscription?.plan ?? SubscriptionPlan.Trial;
  const isPaymentConfigured = pricingCatalog == null || pricingCatalog.plans.length > 0;

  const { reactivateMutation, cancelDowngradeMutation } = useBillingPageMutations({
    startPolling,
    setIsReactivateDialogOpen,
    setIsCancelDowngradeDialogOpen
  });

  const isCancelled = subscription?.status === SubscriptionStatus.Cancelled;
  const isPastDue = subscription?.status === SubscriptionStatus.PastDue;
  const isSubscribed = subscription?.status !== SubscriptionStatus.Trial;
  const scheduledPlan = subscription?.scheduledPlan ?? null;
  const currentPeriodEnd = subscription?.currentPeriodEnd ?? null;
  const formattedPeriodEndLong = formatLongDate(currentPeriodEnd);
  const formattedGracePeriodEnd = formatLongDate(subscription?.gracePeriodEndsAt ?? null);

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
          {isPastDue && <PastDueBanner formattedGracePeriodEnd={formattedGracePeriodEnd} />}
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
          onSubscribe={() => navigate({ to: "/account/billing/subscription" })}
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
    </>
  );
}

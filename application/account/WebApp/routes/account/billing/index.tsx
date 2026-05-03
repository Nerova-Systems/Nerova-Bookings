import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { requirePermission, requireSubscriptionEnabled } from "@repo/infrastructure/auth/routeGuards";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Separator } from "@repo/ui/components/Separator";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { useFormatLongDate } from "@repo/ui/hooks/useSmartDate";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";

import { api, SubscriptionPlan } from "@/shared/lib/api/client";

import { BillingHistoryTable } from "./-components/BillingHistoryTable";
import { BillingInfoSection } from "./-components/BillingInfoSection";
import { BillingPageDialogs } from "./-components/BillingPageDialogs";
import { BillingTabNavigation } from "./-components/BillingTabNavigation";
import { CurrentPlanSection } from "./-components/CurrentPlanSection";
import { InitialPlanSelection } from "./-components/InitialPlanSelection";
import { PaymentMethodSection } from "./-components/PaymentMethodSection";
import { CancellationBanner } from "./-components/SubscriptionBanner";
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
  const formatLongDate = useFormatLongDate();
  const { isPolling, isLoading, startPolling, subscription } = useSubscriptionPolling();
  const [isReactivateDialogOpen, setIsReactivateDialogOpen] = useState(false);
  const [isEditBillingInfoOpen, setIsEditBillingInfoOpen] = useState(false);
  const [isUpdatePaymentMethodOpen, setIsUpdatePaymentMethodOpen] = useState(false);
  const [isRetryPaymentOpen, setIsRetryPaymentOpen] = useState(false);
  const [retryInvoice] = useState({ amount: 0, currency: "" });
  const [isCheckoutDialogOpen, setIsCheckoutDialogOpen] = useState(false);
  const [checkoutPlan, setCheckoutPlan] = useState<SubscriptionPlan>(SubscriptionPlan.Basis);
  const [pendingCheckoutPlan, setPendingCheckoutPlan] = useState<SubscriptionPlan | null>(null);

  const { data: tenant } = api.useQuery("get", "/api/account/tenants/current");
  const { data: pricingCatalog } = api.useQuery("get", "/api/account/subscriptions/pricing-catalog");
  const currentPlan = subscription?.plan ?? SubscriptionPlan.Basis;

  const { reactivateMutation } = useBillingPageMutations({
    startPolling,
    setIsReactivateDialogOpen
  });

  const isPaystackConfigured = (pricingCatalog?.plans?.length ?? 0) > 0;
  const cancelAtPeriodEnd = subscription?.cancelAtPeriodEnd ?? false;
  const currentPeriodEnd = subscription?.currentPeriodEnd ?? null;
  const hasPaystackCustomer = subscription?.hasPaystackCustomer ?? false;
  const formattedPeriodEndLong = formatLongDate(currentPeriodEnd);

  const handleBillingInfoSuccess = () => {
    if (pendingCheckoutPlan == null) return;
    setCheckoutPlan(pendingCheckoutPlan);
    setPendingCheckoutPlan(null);
    setIsCheckoutDialogOpen(true);
  };

  if (isLoading) {
    return (
      <AppLayout variant="center" maxWidth="64rem" title={t`Billing`}>
        <Skeleton className="h-6 w-48" />
      </AppLayout>
    );
  }

  return (
    <>
      {hasPaystackCustomer ? (
        <AppLayout
          variant="center"
          maxWidth="64rem"
          title={t`Billing`}
          subtitle={t`Manage your payment methods and billing information.`}
        >
          <BillingTabNavigation activeTab="billing" />
          {cancelAtPeriodEnd && (
            <CancellationBanner
              currentPlan={currentPlan}
              formattedPeriodEnd={formattedPeriodEndLong}
              onReactivate={() => setIsReactivateDialogOpen(true)}
            />
          )}
          <CurrentPlanSection
            currentPlan={currentPlan}
            cancelAtPeriodEnd={cancelAtPeriodEnd}
            formattedPeriodEndLong={formattedPeriodEndLong}
            currentPriceAmount={subscription?.currentPriceAmount}
            currentPriceCurrency={subscription?.currentPriceCurrency}
            plans={pricingCatalog?.plans}
          />
          <PaymentMethodSection
            paymentMethod={subscription?.paymentMethod}
            isPaystackConfigured={isPaystackConfigured}
            onUpdateClick={() => setIsUpdatePaymentMethodOpen(true)}
          />
          <BillingInfoSection
            billingInfo={subscription?.billingInfo}
            isPaystackConfigured={isPaystackConfigured}
            onEditClick={() => setIsEditBillingInfoOpen(true)}
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
          isPaystackConfigured={isPaystackConfigured}
          onSubscribe={(plan) => {
            setPendingCheckoutPlan(plan);
            setIsEditBillingInfoOpen(true);
          }}
        />
      )}
      <BillingPageDialogs
        currentPlan={currentPlan}
        isReactivateDialogOpen={isReactivateDialogOpen}
        setIsReactivateDialogOpen={setIsReactivateDialogOpen}
        onReactivateConfirm={() => reactivateMutation.mutate({})}
        isReactivatePending={reactivateMutation.isPending || isPolling}
        isEditBillingInfoOpen={isEditBillingInfoOpen}
        setIsEditBillingInfoOpen={setIsEditBillingInfoOpen}
        billingInfo={subscription?.billingInfo}
        tenantName={tenant?.name ?? ""}
        onBillingInfoSuccess={handleBillingInfoSuccess}
        pendingCheckoutPlan={pendingCheckoutPlan}
        isUpdatePaymentMethodOpen={isUpdatePaymentMethodOpen}
        setIsUpdatePaymentMethodOpen={setIsUpdatePaymentMethodOpen}
        isRetryPaymentOpen={isRetryPaymentOpen}
        setIsRetryPaymentOpen={setIsRetryPaymentOpen}
        paymentMethod={subscription?.paymentMethod}
        retryInvoiceAmount={retryInvoice.amount}
        retryInvoiceCurrency={retryInvoice.currency}
        isCheckoutDialogOpen={isCheckoutDialogOpen}
        setIsCheckoutDialogOpen={setIsCheckoutDialogOpen}
        checkoutPlan={checkoutPlan}
      />
    </>
  );
}

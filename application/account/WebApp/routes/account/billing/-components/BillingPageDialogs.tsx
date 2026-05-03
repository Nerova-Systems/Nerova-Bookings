import { t } from "@lingui/core/macro";

import type { components } from "@/shared/lib/api/api.generated";
import type { SubscriptionPlan } from "@/shared/lib/api/client";

import { CancelDowngradeDialog } from "./CancelDowngradeDialog";
import { CheckoutDialog } from "./CheckoutDialog";
import { EditBillingInfoDialog } from "./EditBillingInfoDialog";
import { ReactivateConfirmationDialog } from "./ReactivateConfirmationDialog";
import { RetryPaymentDialog } from "./RetryPaymentDialog";
import { UpdatePaymentMethodDialog } from "./UpdatePaymentMethodDialog";

type BillingInfo = components["schemas"]["BillingInfo"];
type PaymentMethod = components["schemas"]["PaymentMethod"];

interface BillingPageDialogsProps {
  scheduledPlan: SubscriptionPlan | null;
  isCancelDowngradeDialogOpen: boolean;
  setIsCancelDowngradeDialogOpen: (open: boolean) => void;
  onCancelDowngradeConfirm: () => void;
  isCancelDowngradePending: boolean;
  currentPlan: SubscriptionPlan;
  currentPeriodEnd: string | null;

  isReactivateDialogOpen: boolean;
  setIsReactivateDialogOpen: (open: boolean) => void;
  onReactivateConfirm: () => void;
  isReactivatePending: boolean;

  isEditBillingInfoOpen: boolean;
  setIsEditBillingInfoOpen: (open: boolean) => void;
  billingInfo: BillingInfo | null | undefined;
  tenantName: string;
  onBillingInfoSuccess: () => void;
  pendingCheckoutPlan: SubscriptionPlan | null;

  isUpdatePaymentMethodOpen: boolean;
  setIsUpdatePaymentMethodOpen: (open: boolean) => void;

  isRetryPaymentOpen: boolean;
  setIsRetryPaymentOpen: (open: boolean) => void;
  paymentMethod: PaymentMethod | null | undefined;
  retryInvoiceAmount: number;
  retryInvoiceCurrency: string;

  isCheckoutDialogOpen: boolean;
  setIsCheckoutDialogOpen: (open: boolean) => void;
  checkoutPlan: SubscriptionPlan;
}

export function BillingPageDialogs({
  scheduledPlan,
  isCancelDowngradeDialogOpen,
  setIsCancelDowngradeDialogOpen,
  onCancelDowngradeConfirm,
  isCancelDowngradePending,
  currentPlan,
  currentPeriodEnd,
  isReactivateDialogOpen,
  setIsReactivateDialogOpen,
  onReactivateConfirm,
  isReactivatePending,
  isEditBillingInfoOpen,
  setIsEditBillingInfoOpen,
  billingInfo,
  tenantName,
  onBillingInfoSuccess,
  pendingCheckoutPlan,
  isUpdatePaymentMethodOpen,
  setIsUpdatePaymentMethodOpen,
  isRetryPaymentOpen,
  setIsRetryPaymentOpen,
  paymentMethod,
  retryInvoiceAmount,
  retryInvoiceCurrency,
  isCheckoutDialogOpen,
  setIsCheckoutDialogOpen,
  checkoutPlan
}: Readonly<BillingPageDialogsProps>) {
  return (
    <>
      {scheduledPlan && (
        <CancelDowngradeDialog
          isOpen={isCancelDowngradeDialogOpen}
          onOpenChange={setIsCancelDowngradeDialogOpen}
          onConfirm={onCancelDowngradeConfirm}
          isPending={isCancelDowngradePending}
          currentPlan={currentPlan}
          scheduledPlan={scheduledPlan}
          currentPeriodEnd={currentPeriodEnd}
        />
      )}

      <ReactivateConfirmationDialog
        isOpen={isReactivateDialogOpen}
        onOpenChange={setIsReactivateDialogOpen}
        onConfirm={onReactivateConfirm}
        isPending={isReactivatePending}
        currentPlan={currentPlan}
      />

      <EditBillingInfoDialog
        isOpen={isEditBillingInfoOpen}
        onOpenChange={setIsEditBillingInfoOpen}
        billingInfo={billingInfo}
        tenantName={tenantName}
        onSuccess={onBillingInfoSuccess}
        submitLabel={pendingCheckoutPlan != null ? t`Next` : undefined}
        pendingLabel={pendingCheckoutPlan != null ? t`Saving...` : undefined}
      />

      <UpdatePaymentMethodDialog isOpen={isUpdatePaymentMethodOpen} onOpenChange={setIsUpdatePaymentMethodOpen} />

      {isRetryPaymentOpen && (
        <RetryPaymentDialog
          isOpen={isRetryPaymentOpen}
          onOpenChange={setIsRetryPaymentOpen}
          billingInfo={billingInfo}
          paymentMethod={paymentMethod}
          amount={retryInvoiceAmount}
          currency={retryInvoiceCurrency}
        />
      )}

      <CheckoutDialog isOpen={isCheckoutDialogOpen} onOpenChange={setIsCheckoutDialogOpen} plan={checkoutPlan} />
    </>
  );
}

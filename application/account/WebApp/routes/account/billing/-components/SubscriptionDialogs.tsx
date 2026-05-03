import { t } from "@lingui/core/macro";

import type { components } from "@/shared/lib/api/api.generated";
import type { CancellationReason, SubscriptionPlan } from "@/shared/lib/api/client";

import { CancelSubscriptionDialog } from "./CancelSubscriptionDialog";
import { CheckoutDialog } from "./CheckoutDialog";
import { EditBillingInfoDialog } from "./EditBillingInfoDialog";
import { ReactivateConfirmationDialog } from "./ReactivateConfirmationDialog";
import { SubscribeConfirmationDialog } from "./SubscribeConfirmationDialog";
import { UpgradeConfirmationDialog } from "./UpgradeConfirmationDialog";

type BillingInfo = components["schemas"]["BillingInfo"];
type PaymentMethod = components["schemas"]["PaymentMethod"];

interface SubscriptionDialogsProps {
  isCancelDialogOpen: boolean;
  setIsCancelDialogOpen: (open: boolean) => void;
  onCancelConfirm: (reason: CancellationReason, feedback: string | null) => void;
  isCancelPending: boolean;
  currentPeriodEnd: string | null;

  isUpgradeDialogOpen: boolean;
  setIsUpgradeDialogOpen: (open: boolean) => void;
  onUpgradeConfirm: () => void;
  isUpgradePending: boolean;
  upgradeTarget: SubscriptionPlan;

  isSubscribeDialogOpen: boolean;
  setIsSubscribeDialogOpen: (open: boolean) => void;
  onSubscribeConfirm: () => void;
  isSubscribePending: boolean;
  subscribeTarget: SubscriptionPlan;

  currentPlan: SubscriptionPlan;

  isReactivateDialogOpen: boolean;
  setIsReactivateDialogOpen: (open: boolean) => void;
  onReactivateConfirm: () => void;
  isReactivatePending: boolean;

  isEditBillingInfoOpen: boolean;
  setIsEditBillingInfoOpen: (open: boolean) => void;
  billingInfo: BillingInfo | null | undefined;
  paymentMethod: PaymentMethod | null | undefined;
  tenantName: string;
  onBillingInfoSuccess: () => void;

  isCheckoutDialogOpen: boolean;
  setIsCheckoutDialogOpen: (open: boolean) => void;
  checkoutPlan: SubscriptionPlan;
}

export function SubscriptionDialogs({
  isCancelDialogOpen,
  setIsCancelDialogOpen,
  onCancelConfirm,
  isCancelPending,
  currentPeriodEnd,
  isUpgradeDialogOpen,
  setIsUpgradeDialogOpen,
  onUpgradeConfirm,
  isUpgradePending,
  upgradeTarget,
  isSubscribeDialogOpen,
  setIsSubscribeDialogOpen,
  onSubscribeConfirm,
  isSubscribePending,
  subscribeTarget,
  currentPlan,
  isReactivateDialogOpen,
  setIsReactivateDialogOpen,
  onReactivateConfirm,
  isReactivatePending,
  isEditBillingInfoOpen,
  setIsEditBillingInfoOpen,
  billingInfo,
  paymentMethod,
  tenantName,
  onBillingInfoSuccess,
  isCheckoutDialogOpen,
  setIsCheckoutDialogOpen,
  checkoutPlan
}: Readonly<SubscriptionDialogsProps>) {
  return (
    <>
      <CancelSubscriptionDialog
        isOpen={isCancelDialogOpen}
        onOpenChange={setIsCancelDialogOpen}
        onConfirm={onCancelConfirm}
        isPending={isCancelPending}
        currentPeriodEnd={currentPeriodEnd}
      />

      <UpgradeConfirmationDialog
        isOpen={isUpgradeDialogOpen}
        onOpenChange={setIsUpgradeDialogOpen}
        onConfirm={onUpgradeConfirm}
        isPending={isUpgradePending}
        targetPlan={upgradeTarget}
        billingInfo={billingInfo}
        paymentMethod={paymentMethod}
      />

      <SubscribeConfirmationDialog
        isOpen={isSubscribeDialogOpen}
        onOpenChange={setIsSubscribeDialogOpen}
        onConfirm={onSubscribeConfirm}
        isPending={isSubscribePending}
        targetPlan={subscribeTarget}
        billingInfo={billingInfo}
        paymentMethod={paymentMethod}
      />

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
        submitLabel={t`Next`}
        pendingLabel={t`Saving...`}
      />

      <CheckoutDialog isOpen={isCheckoutDialogOpen} onOpenChange={setIsCheckoutDialogOpen} plan={checkoutPlan} />
    </>
  );
}

import type { components } from "@/shared/lib/api/api.generated";

import type { CancellationReason, SubscriptionPlan } from "@/shared/lib/api/client";

import { CancelDowngradeDialog } from "./CancelDowngradeDialog";
import { CancelSubscriptionDialog } from "./CancelSubscriptionDialog";
import { DowngradeConfirmationDialog } from "./DowngradeConfirmationDialog";
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
  billingInfo: BillingInfo | null | undefined;
  paymentMethod: PaymentMethod | null | undefined;

  isDowngradeDialogOpen: boolean;
  setIsDowngradeDialogOpen: (open: boolean) => void;
  onDowngradeConfirm: () => void;
  isDowngradePending: boolean;
  downgradeTarget: SubscriptionPlan;

  scheduledPlan: SubscriptionPlan | null;
  isCancelDowngradeDialogOpen: boolean;
  setIsCancelDowngradeDialogOpen: (open: boolean) => void;
  onCancelDowngradeConfirm: () => void;
  isCancelDowngradePending: boolean;
  currentPlan: SubscriptionPlan;

  isReactivateDialogOpen: boolean;
  setIsReactivateDialogOpen: (open: boolean) => void;
  onReactivateConfirm: () => void;
  isReactivatePending: boolean;
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
  billingInfo,
  paymentMethod,
  isDowngradeDialogOpen,
  setIsDowngradeDialogOpen,
  onDowngradeConfirm,
  isDowngradePending,
  downgradeTarget,
  scheduledPlan,
  isCancelDowngradeDialogOpen,
  setIsCancelDowngradeDialogOpen,
  onCancelDowngradeConfirm,
  isCancelDowngradePending,
  currentPlan,
  isReactivateDialogOpen,
  setIsReactivateDialogOpen,
  onReactivateConfirm,
  isReactivatePending
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

      <DowngradeConfirmationDialog
        isOpen={isDowngradeDialogOpen}
        onOpenChange={setIsDowngradeDialogOpen}
        onConfirm={onDowngradeConfirm}
        isPending={isDowngradePending}
        targetPlan={downgradeTarget}
        currentPeriodEnd={currentPeriodEnd}
      />

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
    </>
  );
}

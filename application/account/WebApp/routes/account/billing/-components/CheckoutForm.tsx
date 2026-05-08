import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { DialogClose, DialogFooter } from "@repo/ui/components/Dialog";
import { Separator } from "@repo/ui/components/Separator";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { formatCurrency } from "@repo/utils/currency/formatCurrency";
import { useState } from "react";
import { toast } from "sonner";

import {
  api,
  type PaystackPaymentPurpose,
  type SubscriptionPlan as SubscriptionPlanType
} from "@/shared/lib/api/client";

import { resumePaystackTransaction } from "./paystackInline";
import { getPlanDetails } from "./planUtils";

export interface PaystackCheckoutPayment {
  accessCode: string | null;
  reference: string | null;
  amount: number | null;
  currency: string | null;
  operationPurpose: PaystackPaymentPurpose;
}

interface CheckoutFormProps {
  plan: SubscriptionPlanType;
  payment: PaystackCheckoutPayment;
  onPaymentCompleted: (reference: string) => void;
  onError: (error: string) => void;
}

function getDisplayError(message: string | undefined): string {
  if (!message) {
    return t`An error occurred while processing your payment.`;
  }
  return message;
}

export function CheckoutForm({ plan, payment, onPaymentCompleted, onError }: Readonly<CheckoutFormProps>) {
  const [isPaystackOpen, setIsPaystackOpen] = useState(false);

  const { data: preview } = api.useQuery("get", "/api/account/subscriptions/checkout-preview", {
    params: { query: { Plan: plan } }
  });

  const planDetails = getPlanDetails(plan);

  const handleSubmit = async () => {
    if (!payment.accessCode) {
      return;
    }

    setIsPaystackOpen(true);
    onError("");

    try {
      await resumePaystackTransaction(payment.accessCode, {
        onSuccess: (transaction) => {
          const reference = transaction.reference ?? transaction.trxref ?? payment.reference;
          setIsPaystackOpen(false);
          if (!reference) {
            const errorMessage = t`Payment completed but no Paystack reference was returned.`;
            onError(errorMessage);
            toast.error(errorMessage);
            return;
          }
          onPaymentCompleted(reference);
        },
        onCancel: () => {
          setIsPaystackOpen(false);
        },
        onError: (error) => {
          setIsPaystackOpen(false);
          const errorMessage = getDisplayError(error.message);
          onError(errorMessage);
          toast.error(errorMessage);
        }
      });
    } catch (error) {
      setIsPaystackOpen(false);
      const message = error instanceof Error ? error.message : undefined;
      const errorMessage = getDisplayError(message);
      onError(errorMessage);
      toast.error(errorMessage);
    }
  };

  return (
    <>
      <CheckoutSummary preview={preview} payment={payment} planName={planDetails.name} />
      <p className="text-xs text-muted-foreground">
        <Trans>
          By subscribing, you agree to our{" "}
          <a href="/legal/terms" className="underline" target="_blank" rel="noopener noreferrer">
            terms of service
          </a>{" "}
          and{" "}
          <a href="/legal/privacy" className="underline" target="_blank" rel="noopener noreferrer">
            privacy policy
          </a>
          .
        </Trans>
      </p>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={isPaystackOpen} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button onClick={handleSubmit} disabled={isPaystackOpen || !preview || !payment.accessCode}>
          {isPaystackOpen ? <Trans>Processing payment...</Trans> : <Trans>Pay and subscribe</Trans>}
        </Button>
      </DialogFooter>
    </>
  );
}

function CheckoutSummary({
  preview,
  payment,
  planName
}: Readonly<{
  preview: { totalAmount: number; taxAmount: number; currency: string } | undefined;
  payment: PaystackCheckoutPayment;
  planName: string;
}>) {
  const totalAmount = preview?.totalAmount ?? payment.amount ?? 0;
  const taxAmount = preview?.taxAmount ?? 0;
  const currency = preview?.currency ?? payment.currency ?? "USD";

  return (
    <div className="mb-2 flex flex-col gap-2">
      {preview || payment.amount != null ? (
        <>
          <div className="flex items-baseline justify-between gap-4 text-sm">
            <span className="text-muted-foreground">{planName}</span>
            <span className="shrink-0 whitespace-nowrap text-muted-foreground tabular-nums">
              {formatCurrency(totalAmount - taxAmount, currency)}
            </span>
          </div>
          <div className="flex items-baseline justify-between gap-4 text-sm">
            <span className="text-muted-foreground">
              <Trans>Tax</Trans>
            </span>
            <span className="shrink-0 whitespace-nowrap text-muted-foreground tabular-nums">
              {formatCurrency(taxAmount, currency)}
            </span>
          </div>
          <Separator />
          <div className="flex items-baseline justify-between gap-4 font-medium">
            <span>
              <Trans>Total</Trans>
            </span>
            <span className="shrink-0 text-lg whitespace-nowrap tabular-nums">
              {formatCurrency(totalAmount, currency)}
            </span>
          </div>
        </>
      ) : (
        <>
          <div className="flex items-center justify-between">
            <Skeleton className="h-4 w-[10rem]" />
            <Skeleton className="h-4 w-[4rem]" />
          </div>
          <Separator />
          <div className="flex items-center justify-between">
            <Skeleton className="h-5 w-[6rem]" />
            <Skeleton className="h-5 w-[5rem]" />
          </div>
        </>
      )}
    </div>
  );
}

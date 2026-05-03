import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { buttonVariants } from "@repo/ui/components/Button";
import { createFileRoute, Link, useLocation } from "@tanstack/react-router";
import { useEffect } from "react";

import { useConfirmPaystackReference } from "@/shared/lib/publicBookingApi";

export const Route = createFileRoute("/book/payment/callback")({
  component: PaystackCallbackPage
});

function PaystackCallbackPage() {
  const location = useLocation();
  const params = new URLSearchParams(location.searchStr);
  const reference = params.get("reference") ?? params.get("trxref") ?? "";
  const isSubscriptionReference = reference.startsWith("NB-sub-");
  const confirmationQuery = useConfirmPaystackReference(isSubscriptionReference ? "" : reference);
  const appointmentReference = confirmationQuery.data?.appointmentReference;

  useEffect(() => {
    document.title = t`Payment confirmation | Nerova`;
  }, []);

  useEffect(() => {
    if (!isSubscriptionReference) {
      return;
    }

    const subscriptionUrl = new URL("/account/billing/subscription", window.location.origin);
    subscriptionUrl.searchParams.set("reference", reference);
    window.location.replace(subscriptionUrl.toString());
  }, [isSubscriptionReference, reference]);

  return (
    <main className="flex min-h-screen items-center justify-center bg-muted px-6 text-foreground">
      <section className="w-full max-w-lg rounded-xl border border-border bg-background p-8 text-center">
        <div className="mb-2 text-xs font-semibold tracking-[0.12em] text-muted-foreground uppercase">Paystack</div>
        <h1 className="font-display text-3xl font-semibold">
          {confirmationQuery.isError ? <Trans>Payment needs review</Trans> : <Trans>Verifying payment</Trans>}
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          <Trans>The payment return is verified server-side before the appointment is marked paid.</Trans>
        </p>

        <div className="mt-6 rounded-lg bg-muted px-4 py-4 text-sm">
          {confirmationQuery.isLoading && <Trans>Checking transaction reference...</Trans>}
          {confirmationQuery.isError && <Trans>We could not verify this payment reference yet.</Trans>}
          {appointmentReference && <span>Payment verified for booking {appointmentReference}.</span>}
        </div>

        <div className="mt-7 flex justify-center">
          {appointmentReference ? (
            <Link
              to="/book/confirmation/$reference"
              params={{ reference: appointmentReference }}
              className={buttonVariants()}
            >
              <Trans>View booking</Trans>
            </Link>
          ) : (
            <span className={buttonVariants({ className: "pointer-events-none opacity-50" })}>
              <Trans>View booking</Trans>
            </span>
          )}
        </div>
      </section>
    </main>
  );
}

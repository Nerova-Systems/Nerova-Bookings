import type { ReactNode } from "react";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, Link } from "@tanstack/react-router";
import { CalendarDaysIcon, CreditCardIcon, Loader2Icon, MapPinIcon, ShieldCheckIcon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

import { money } from "@/shared/lib/appointmentsApi";
import { useInitializePublicPayment, usePublicPaymentDetails } from "@/shared/lib/publicBookingApi";

export const Route = createFileRoute("/pay/$token")({
  staticData: { trackingTitle: "Public payment" },
  component: PublicPaymentPage
});

declare global {
  interface Window {
    Paystack?: PaystackInlineConstructor;
    PaystackPop?: PaystackInlineConstructor;
  }
}

interface PaystackInlineConstructor {
  new (): {
    resumeTransaction: (accessCode: string) => void;
  };
}

const PAYSTACK_INLINE_SCRIPT_ID = "paystack-inline-v2";
const PAYSTACK_INLINE_SCRIPT_SRC = "https://js.paystack.co/v2/inline.js";

function PublicPaymentPage() {
  const { token } = Route.useParams();
  const paymentQuery = usePublicPaymentDetails(token);
  const initializePayment = useInitializePublicPayment(token);
  const [checkoutUrl, setCheckoutUrl] = useState<string | undefined>();
  const [isOpeningCheckout, setIsOpeningCheckout] = useState(false);

  useEffect(() => {
    document.title = t`Pay appointment | Nerova`;
  }, []);

  const details = paymentQuery.data;
  const accentColor = details?.business.brandColor ?? "#111827";
  const initials = useMemo(() => businessInitials(details?.business.name), [details?.business.name]);

  const startPayment = async () => {
    setIsOpeningCheckout(true);
    try {
      const result = await initializePayment.mutateAsync();
      setCheckoutUrl(result.authorizationUrl);
      await loadPaystackInline();
      const PaystackInline = window.PaystackPop ?? window.Paystack;
      if (!PaystackInline) throw new Error("Payment checkout could not load.");
      new PaystackInline().resumeTransaction(result.accessCode);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Could not open secure payment.");
    } finally {
      setIsOpeningCheckout(false);
    }
  };

  if (paymentQuery.isLoading) {
    return (
      <main className="flex min-h-dvh items-center justify-center bg-[#101010] text-white">
        <Loader2Icon className="size-5 animate-spin" />
      </main>
    );
  }

  if (paymentQuery.isError || !details) {
    return (
      <main className="flex min-h-dvh items-center justify-center bg-[#101010] px-5 text-white">
        <section className="w-full max-w-lg rounded-2xl border border-white/10 bg-[#191919] p-8 text-center">
          <h1 className="font-display text-3xl font-semibold">
            <Trans>Payment link unavailable</Trans>
          </h1>
          <p className="mt-3 text-sm leading-6 text-white/60">
            <Trans>This payment link is invalid, expired, or no longer payable.</Trans>
          </p>
        </section>
      </main>
    );
  }

  const startAt = new Date(details.appointment.startAt);
  const endAt = new Date(details.appointment.endAt);
  const isPaid = details.payment.status.toLowerCase() === "paid";
  const isBusy = initializePayment.isPending || isOpeningCheckout;

  return (
    <main className="min-h-dvh bg-muted text-foreground">
      <div className="mx-auto grid min-h-dvh max-w-6xl grid-cols-[minmax(18rem,23rem)_1fr] bg-background shadow-[0_0_80px_rgba(24,24,27,0.10)] max-lg:block dark:shadow-none">
        <aside className="relative overflow-hidden border-r border-border bg-[#181818] p-8 text-white max-lg:border-r-0 max-lg:p-6">
          <div className="absolute inset-x-0 top-0 h-48 bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.26),transparent_45%)]" />
          <div className="relative flex min-h-[calc(100vh-4rem)] flex-col justify-between gap-8 max-lg:min-h-0">
            <div>
              <BusinessLogo logoUrl={details.business.logoUrl} name={details.business.name} initials={initials} />
              <div className="mt-6 text-xs font-semibold tracking-[0.16em] text-white/50 uppercase">
                <Trans>Secure payment</Trans>
              </div>
              <h1 className="mt-2 font-display text-4xl font-semibold tracking-tight">{details.business.name}</h1>
              <p className="mt-3 max-w-xs text-sm leading-6 text-white/65">
                <Trans>Review your booking details before opening the secure checkout.</Trans>
              </p>
            </div>

            <div className="grid gap-4">
              <div className="rounded-2xl border border-white/10 bg-white/[0.08] p-4">
                <div className="text-xs font-semibold tracking-[0.14em] text-white/50 uppercase">
                  <Trans>Amount due</Trans>
                </div>
                <div className="mt-3 font-display text-3xl font-semibold">
                  {formatAmount(details.payment.amountCents, details.payment.currency)}
                </div>
                <div className="mt-2 text-xs text-white/55">
                  <Trans>Reference</Trans> {details.appointment.reference}
                </div>
              </div>
              <div className="text-xs text-white/45">
                <Trans>Powered by Nerova</Trans>
              </div>
            </div>
          </div>
        </aside>

        <section className="px-6 py-8 sm:px-8 lg:px-10">
          <header className="mb-8 flex flex-wrap items-start justify-between gap-4 border-b border-border pb-6">
            <div>
              <div className="text-xs font-semibold tracking-[0.16em] text-muted-foreground uppercase">
                <Trans>Appointment payment</Trans>
              </div>
              <h2 className="mt-2 font-display text-3xl font-semibold tracking-tight">
                {details.appointment.serviceName}
              </h2>
            </div>
            <div
              className="rounded-full border px-3 py-1 text-xs font-semibold"
              style={{ borderColor: accentColor, color: accentColor }}
            >
              {details.payment.status}
            </div>
          </header>

          <div className="grid gap-6">
            <div className="grid gap-3 sm:grid-cols-2">
              <PaymentInfo
                icon={<CalendarDaysIcon className="size-4" />}
                label={t`Date and time`}
                value={formatWhen(startAt, endAt)}
              />
              <PaymentInfo
                icon={<MapPinIcon className="size-4" />}
                label={t`Location`}
                value={details.appointment.location}
              />
              <PaymentInfo
                icon={<CreditCardIcon className="size-4" />}
                label={t`Amount`}
                value={formatAmount(details.payment.amountCents, details.payment.currency)}
              />
              <PaymentInfo
                icon={<ShieldCheckIcon className="size-4" />}
                label={t`Link expires`}
                value={formatDateTime(new Date(details.payment.expiresAt))}
              />
            </div>

            <section className="rounded-2xl border border-border bg-muted/35 p-5">
              <div className="flex flex-wrap items-center justify-between gap-4">
                <div>
                  <h3 className="font-display text-xl font-semibold">
                    {isPaid ? <Trans>Payment received</Trans> : <Trans>Ready to pay</Trans>}
                  </h3>
                  <p className="mt-1 max-w-xl text-sm leading-6 text-muted-foreground">
                    {isPaid ? (
                      <Trans>This appointment is already marked paid.</Trans>
                    ) : (
                      <Trans>Your checkout is initialized securely by the business before Paystack opens.</Trans>
                    )}
                  </p>
                </div>
                <Button disabled={isPaid || isBusy} onClick={startPayment}>
                  {isBusy && <Loader2Icon className="size-4 animate-spin" />}
                  <Trans>Pay securely</Trans>
                </Button>
              </div>

              {checkoutUrl && (
                <div className="mt-4 border-t border-border pt-4 text-sm text-muted-foreground">
                  <Trans>If the checkout did not open, continue in the secure checkout window.</Trans>{" "}
                  <a className="font-medium text-foreground underline underline-offset-4" href={checkoutUrl}>
                    <Trans>Open checkout</Trans>
                  </a>
                </div>
              )}

              {isPaid && (
                <div className="mt-4 border-t border-border pt-4 text-sm">
                  <Link
                    className="font-medium underline underline-offset-4"
                    to="/book/confirmation/$reference"
                    params={{ reference: details.appointment.reference }}
                  >
                    <Trans>View booking confirmation</Trans>
                  </Link>
                </div>
              )}
            </section>
          </div>
        </section>
      </div>
    </main>
  );
}

function BusinessLogo({ logoUrl, name, initials }: { logoUrl?: string; name: string; initials: string }) {
  const [imageFailed, setImageFailed] = useState(false);

  if (logoUrl && !imageFailed) {
    return (
      <img
        src={logoUrl}
        alt={`${name} logo`}
        onError={() => setImageFailed(true)}
        className="size-16 rounded-2xl border border-white/15 bg-white object-cover"
      />
    );
  }

  return (
    <div className="flex size-16 items-center justify-center rounded-2xl border border-white/15 bg-white text-xl font-semibold text-[#181818]">
      {initials || "N"}
    </div>
  );
}

function PaymentInfo({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-border bg-background p-5">
      <div className="flex items-center gap-2 text-xs font-semibold tracking-[0.12em] text-muted-foreground uppercase">
        {icon}
        {label}
      </div>
      <div className="mt-3 text-sm leading-6 font-semibold">{value}</div>
    </div>
  );
}

function businessInitials(name?: string) {
  return (name ?? "")
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
}

function formatAmount(amountCents: number, currency: string) {
  if (currency.toUpperCase() === "ZAR") {
    return money(amountCents);
  }

  return `${currency.toUpperCase()} ${money(amountCents)}`;
}

function formatWhen(start: Date, end: Date) {
  const day = new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    year: "numeric"
  }).format(start);
  const time = new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).formatRange(start, end);
  return `${day}, ${time}`;
}

function formatDateTime(value: Date) {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(value);
}

async function loadPaystackInline() {
  if (window.PaystackPop || window.Paystack) return;

  const existingScript = document.getElementById(PAYSTACK_INLINE_SCRIPT_ID) as HTMLScriptElement | null;
  if (existingScript) {
    await waitForScript(existingScript);
    return;
  }

  const script = document.createElement("script");
  script.id = PAYSTACK_INLINE_SCRIPT_ID;
  script.src = PAYSTACK_INLINE_SCRIPT_SRC;
  script.async = true;
  document.body.append(script);
  await waitForScript(script);
}

function waitForScript(script: HTMLScriptElement) {
  return new Promise<void>((resolve, reject) => {
    if (script.dataset.loaded === "true") {
      resolve();
      return;
    }

    script.addEventListener(
      "load",
      () => {
        script.dataset.loaded = "true";
        resolve();
      },
      { once: true }
    );
    script.addEventListener("error", () => reject(new Error("Payment checkout could not load.")), { once: true });
  });
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { buttonVariants } from "@repo/ui/components/Button";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect } from "react";

import { money } from "@/shared/lib/appointmentsApi";
import { formatDayGroup, formatTime } from "@/shared/lib/dateFormatting";
import { usePublicConfirmation } from "@/shared/lib/publicBookingApi";

export const Route = createFileRoute("/book/confirmation/$reference")({
  component: BookingConfirmationPage
});

function BookingConfirmationPage() {
  const { reference } = Route.useParams();
  const confirmationQuery = usePublicConfirmation(reference);

  useEffect(() => {
    document.title = t`Booking confirmation | Nerova`;
  }, []);

  const appointment = confirmationQuery.data;

  return (
    <main className="flex min-h-screen items-center justify-center bg-[#f7f7f5] px-6 text-foreground">
      <section className="w-full max-w-xl rounded-xl border border-border bg-background p-8">
        <div className="mb-6">
          <div className="mb-2 text-xs font-semibold tracking-[0.12em] text-muted-foreground uppercase">Nerova</div>
          <h1 className="font-display text-3xl font-semibold">
            <Trans>Booking received</Trans>
          </h1>
          <p className="mt-2 text-sm text-muted-foreground">
            <Trans>Your appointment request has been saved.</Trans>
          </p>
        </div>

        {!appointment ? (
          <div className="rounded-lg bg-muted px-4 py-6 text-sm text-muted-foreground">
            <Trans>Loading confirmation...</Trans>
          </div>
        ) : (
          <div className="space-y-3 text-sm">
            <Row label="Reference" value={appointment.publicReference} />
            <Row label="Service" value={appointment.serviceName} />
            <Row label="When" value={formatRange(appointment.startAt, appointment.endAt)} />
            <Row label="Where" value={appointment.location} />
            <Row label="Amount" value={money(appointment.priceCents)} />
            <Row label="Status" value={`${appointment.status} · ${appointment.paymentStatus}`} />
          </div>
        )}

        <div className="mt-7 flex justify-end">
          <Link to="/" className={buttonVariants()}>
            <Trans>Done</Trans>
          </Link>
        </div>
      </section>
    </main>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[8rem_1fr] gap-3 border-b border-border py-2 last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium">{value}</span>
    </div>
  );
}

function formatRange(startAt: string, endAt: string) {
  const start = new Date(startAt);
  const end = new Date(endAt);
  const date = formatDayGroup(start);
  const startTime = formatTime(start);
  const endTime = formatTime(end);
  return `${date} · ${startTime}-${endTime}`;
}

import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { useState } from "react";

import type {
  Service,
  ServiceCategory,
  ServiceMutationRequest,
  ServicePaymentPolicy
} from "@/shared/lib/appointmentsApi";

interface ServiceFormDialogProps {
  service?: Service;
  categories: ServiceCategory[];
  pending: boolean;
  onClose: () => void;
  onSubmit: (request: ServiceMutationRequest) => void;
}

export function ServiceFormDialog({ service, categories, pending, onClose, onSubmit }: ServiceFormDialogProps) {
  const category =
    categories.find((item) => item.id === service?.categoryId)?.name ?? categories[0]?.name ?? "Consultations";
  const [form, setForm] = useState({
    name: service?.name ?? "",
    categoryName: category,
    mode: service?.mode ?? "physical",
    durationMinutes: service?.durationMinutes.toString() ?? "60",
    price: centsToRand(service?.priceCents ?? 45000),
    deposit: centsToRand(service?.depositCents ?? 0),
    paymentPolicy: service?.paymentPolicy ?? (service?.depositCents ? "DepositBeforeBooking" : "NoPaymentRequired"),
    location: service?.location ?? "",
    bufferBeforeMinutes: "0",
    bufferAfterMinutes: "0"
  });

  const submit = () => {
    onSubmit({
      name: form.name.trim(),
      categoryName: form.categoryName.trim(),
      mode: form.mode,
      durationMinutes: Number(form.durationMinutes),
      priceCents: randToCents(form.price),
      depositCents: form.paymentPolicy === "DepositBeforeBooking" ? randToCents(form.deposit) : 0,
      paymentPolicy: form.paymentPolicy,
      bufferBeforeMinutes: Number(form.bufferBeforeMinutes),
      bufferAfterMinutes: Number(form.bufferAfterMinutes),
      location: form.location.trim(),
      description: ""
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/25 p-4 backdrop-blur-sm">
      <div className="w-full max-w-2xl overflow-hidden rounded-lg border border-border bg-background shadow-2xl">
        <div className="border-b border-border px-5 py-4">
          <h2 className="font-display text-lg font-semibold">
            {service ? <Trans>Edit service</Trans> : <Trans>New service</Trans>}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            <Trans>Services control public booking availability, pricing, deposits, and operator scheduling.</Trans>
          </p>
        </div>
        <div className="grid max-h-[70vh] gap-4 overflow-y-auto px-5 py-4 md:grid-cols-2">
          <Field
            label="Service name"
            value={form.name}
            onChange={(name) => setForm((current) => ({ ...current, name }))}
          />
          <Field
            label="Category"
            value={form.categoryName}
            onChange={(categoryName) => setForm((current) => ({ ...current, categoryName }))}
            list="service-categories"
          />
          <datalist id="service-categories">
            {categories.map((item) => (
              <option key={item.id} value={item.name} />
            ))}
          </datalist>
          <label className="grid gap-1.5 text-sm">
            <span className="text-xs font-semibold text-muted-foreground">Mode</span>
            <select
              value={form.mode}
              onChange={(event) =>
                setForm((current) => ({ ...current, mode: event.target.value as ServiceMutationRequest["mode"] }))
              }
              className="h-10 rounded-md border border-border bg-background px-3 text-sm outline-none focus:border-foreground/40"
            >
              <option value="physical">Physical</option>
              <option value="virtual">Virtual</option>
              <option value="mobile">At client</option>
            </select>
          </label>
          <Field
            label="Duration (minutes)"
            value={form.durationMinutes}
            onChange={(durationMinutes) => setForm((current) => ({ ...current, durationMinutes }))}
          />
          <Field
            label="Price (R)"
            value={form.price}
            onChange={(price) => setForm((current) => ({ ...current, price }))}
          />
          <label className="grid gap-1.5 text-sm">
            <span className="text-xs font-semibold text-muted-foreground">Payment rule</span>
            <select
              value={form.paymentPolicy}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  paymentPolicy: event.target.value as ServicePaymentPolicy,
                  deposit: event.target.value === "DepositBeforeBooking" ? current.deposit : "0"
                }))
              }
              className="h-10 rounded-md border border-border bg-background px-3 text-sm outline-none focus:border-foreground/40"
            >
              <option value="NoPaymentRequired">No payment required</option>
              <option value="DepositBeforeBooking">Deposit before booking</option>
              <option value="FullPaymentBeforeBooking">Full payment before booking</option>
              <option value="CollectAfterAppointment">Collect after appointment</option>
            </select>
          </label>
          {form.paymentPolicy === "DepositBeforeBooking" ? (
            <Field
              label="Deposit (R)"
              value={form.deposit}
              onChange={(deposit) => setForm((current) => ({ ...current, deposit }))}
            />
          ) : (
            <div className="rounded-md border border-border bg-muted/50 px-3 py-2 text-sm text-muted-foreground">
              {form.paymentPolicy === "FullPaymentBeforeBooking"
                ? "Client pays the full price before the booking is confirmed."
                : form.paymentPolicy === "CollectAfterAppointment"
                  ? "Staff collect the full price from Activity using Paystack Virtual Terminal."
                  : "Booking does not require appointment payment tracking."}
            </div>
          )}
          <Field
            label="Buffer before (minutes)"
            value={form.bufferBeforeMinutes}
            onChange={(bufferBeforeMinutes) => setForm((current) => ({ ...current, bufferBeforeMinutes }))}
          />
          <Field
            label="Buffer after (minutes)"
            value={form.bufferAfterMinutes}
            onChange={(bufferAfterMinutes) => setForm((current) => ({ ...current, bufferAfterMinutes }))}
          />
          <div className="md:col-span-2">
            <Field
              label="Location"
              value={form.location}
              onChange={(location) => setForm((current) => ({ ...current, location }))}
            />
          </div>
        </div>
        <div className="flex items-center justify-end gap-2 border-t border-border px-5 py-4">
          <Button type="button" variant="outline" onClick={onClose}>
            <Trans>Cancel</Trans>
          </Button>
          <Button
            type="button"
            disabled={pending || form.name.trim() === "" || form.location.trim() === ""}
            onClick={submit}
          >
            {service ? <Trans>Save service</Trans> : <Trans>Create service</Trans>}
          </Button>
        </div>
      </div>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  list
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  list?: string;
}) {
  return (
    <label className="grid gap-1.5 text-sm">
      <span className="text-xs font-semibold text-muted-foreground">{label}</span>
      <input
        value={value}
        list={list}
        onChange={(event) => onChange(event.target.value)}
        className="h-10 rounded-md border border-border bg-background px-3 text-sm outline-none focus:border-foreground/40"
      />
    </label>
  );
}

function centsToRand(cents: number) {
  return String(Math.round(cents / 100));
}

function randToCents(value: string) {
  return Math.max(0, Math.round(Number(value || "0") * 100));
}

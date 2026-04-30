import { Trans } from "@lingui/react/macro";

import { money } from "@/shared/lib/appointmentsApi";
import { formatTime } from "@/shared/lib/dateFormatting";
import type { PublicBookingService, Slot } from "@/shared/lib/publicBookingApi";

import { StepHeading, TextInput } from "./PublicBookingParts";

export function ServicePicker({
  services,
  serviceId,
  onSelect
}: {
  services: PublicBookingService[];
  serviceId?: string;
  onSelect: (serviceId: string) => void;
}) {
  return (
    <section>
      <StepHeading
        step="1"
        title="Choose a service"
        description="Pick the appointment type that matches what you need."
      />
      <div className="grid grid-cols-[repeat(auto-fit,minmax(15rem,1fr))] gap-3">
        {services.map((service) => (
          <button
            key={service.id}
            type="button"
            onClick={() => onSelect(service.id)}
            className={`rounded-2xl border p-4 text-left transition-all hover:-translate-y-0.5 hover:border-foreground/50 ${
              serviceId === service.id
                ? "border-foreground bg-foreground text-background shadow-xl shadow-black/10"
                : "border-border bg-background"
            }`}
          >
            <div className="flex min-h-20 flex-col justify-between">
              <div>
                <div className="font-medium">{service.name}</div>
                <div className="mt-1 text-xs opacity-70">{service.mode} appointment</div>
              </div>
              <div className="mt-4 flex flex-wrap gap-2 text-xs">
                <span className="rounded-full bg-current/10 px-2 py-1">{service.durationMinutes} min</span>
                <span className="rounded-full bg-current/10 px-2 py-1">{money(service.priceCents)}</span>
                {service.paymentPolicy === "DepositBeforeBooking" && service.depositCents > 0 && (
                  <span className="rounded-full bg-current/10 px-2 py-1">{money(service.depositCents)} deposit</span>
                )}
                {service.paymentPolicy === "FullPaymentBeforeBooking" && (
                  <span className="rounded-full bg-current/10 px-2 py-1">Pay before booking</span>
                )}
                {service.paymentPolicy === "CollectAfterAppointment" && (
                  <span className="rounded-full bg-current/10 px-2 py-1">Pay after appointment</span>
                )}
              </div>
            </div>
          </button>
        ))}
      </div>
    </section>
  );
}

export function SlotPicker({
  date,
  slots,
  slotStart,
  onDateChange,
  onSlotSelect
}: {
  date: string;
  slots: Slot[];
  slotStart: string;
  onDateChange: (date: string) => void;
  onSlotSelect: (slotStart: string) => void;
}) {
  return (
    <section>
      <StepHeading
        step="2"
        title="Choose a time"
        description="Available times update from the same calendar used by the business."
      />
      <div className="grid grid-cols-[15rem_1fr] gap-5 max-md:grid-cols-1">
        <label className="text-sm font-medium" htmlFor="booking-date">
          <span className="mb-2 block text-xs font-semibold tracking-[0.12em] text-muted-foreground uppercase">
            Date
          </span>
          <input
            id="booking-date"
            type="date"
            value={date}
            onChange={(event) => onDateChange(event.target.value)}
            className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none transition-colors focus:border-foreground"
          />
        </label>
        <div>
          <h3 className="mb-2 text-xs font-semibold tracking-[0.12em] text-muted-foreground uppercase">
            <Trans>Available slots</Trans>
          </h3>
          <div className="grid grid-cols-[repeat(auto-fill,minmax(7rem,1fr))] gap-2">
            {slots.map((slot) => (
              <button
                key={slot.startAt}
                type="button"
                onClick={() => onSlotSelect(slot.startAt)}
                className={`h-12 rounded-xl border px-3 text-sm font-medium transition-colors ${
                  slotStart === slot.startAt
                    ? "border-foreground bg-foreground text-background"
                    : "border-border bg-background hover:border-foreground/60"
                }`}
              >
                {formatTime(new Date(slot.startAt))}
              </button>
            ))}
            {slots.length === 0 && (
              <div className="col-span-full rounded-xl bg-muted px-4 py-5 text-sm text-muted-foreground">
                <Trans>No open slots for this date.</Trans>
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}

export function BookingFields({
  name,
  phone,
  email,
  note,
  isCheckingPhone,
  onNameChange,
  onPhoneChange,
  onEmailChange,
  onNoteChange
}: {
  name: string;
  phone: string;
  email: string;
  note: string;
  isCheckingPhone: boolean;
  onNameChange: (value: string) => void;
  onPhoneChange: (value: string) => void;
  onEmailChange: (value: string) => void;
  onNoteChange: (value: string) => void;
}) {
  return (
    <section>
      <StepHeading
        step="3"
        title="Your details"
        description="Start with your phone number so returning clients can be filled in quickly."
      />
      <div className="grid gap-3">
        <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] gap-3 max-sm:grid-cols-1">
          <TextInput label="Phone number" value={phone} onChange={onPhoneChange} autoComplete="tel" />
          <TextInput label="Email" value={email} onChange={onEmailChange} autoComplete="email" />
        </div>
        <TextInput label="Name" value={name} onChange={onNameChange} autoComplete="name" />
        <div className="min-h-5 text-xs text-muted-foreground">
          {isCheckingPhone ? (
            <Trans>Checking for saved booking details...</Trans>
          ) : (
            <Trans>Your phone number is used only to prepare this booking.</Trans>
          )}
        </div>
        <label className="text-sm font-medium" htmlFor="booking-note">
          <span className="mb-2 block">Anything we should know?</span>
          <textarea
            id="booking-note"
            value={note}
            onChange={(event) => onNoteChange(event.target.value)}
            className="min-h-28 w-full resize-none rounded-xl border border-border bg-background px-3 py-3 text-sm outline-none transition-colors focus:border-foreground"
          />
        </label>
      </div>
    </section>
  );
}

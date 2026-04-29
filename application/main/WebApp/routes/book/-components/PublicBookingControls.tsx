import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

import { money } from "@/shared/lib/appointmentsApi";
import { formatTime } from "@/shared/lib/dateFormatting";
import type { PublicBookingService, Slot } from "@/shared/lib/publicBookingApi";

import { TextInput } from "./PublicBookingParts";

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
    <div>
      <h3 className="mb-2 text-xs font-semibold tracking-[0.08em] text-muted-foreground uppercase">
        <Trans>Service</Trans>
      </h3>
      <div className="grid grid-cols-[repeat(auto-fill,minmax(14rem,1fr))] gap-2">
        {services.map((service) => (
          <button
            key={service.id}
            type="button"
            onClick={() => onSelect(service.id)}
            className={`rounded-xl border p-4 text-left transition-colors ${
              serviceId === service.id ? "border-foreground bg-foreground text-background" : "border-border"
            }`}
          >
            <div className="font-medium">{service.name}</div>
            <div className="mt-1 text-xs opacity-70">
              {service.durationMinutes} min - {money(service.priceCents)}
              {service.depositCents > 0 ? ` - ${money(service.depositCents)} deposit` : ""}
            </div>
          </button>
        ))}
      </div>
    </div>
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
    <div className="grid grid-cols-[16rem_1fr] gap-5">
      <label className="text-sm font-medium" htmlFor="booking-date">
        <span className="mb-1 block">Date</span>
        <input
          id="booking-date"
          type="date"
          value={date}
          onChange={(event) => onDateChange(event.target.value)}
          className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm"
        />
      </label>
      <div>
        <h3 className="mb-2 text-xs font-semibold tracking-[0.08em] text-muted-foreground uppercase">
          <Trans>Available slots</Trans>
        </h3>
        <div className="grid grid-cols-[repeat(auto-fill,minmax(7.5rem,1fr))] gap-2">
          {slots.map((slot) => (
            <button
              key={slot.startAt}
              type="button"
              onClick={() => onSlotSelect(slot.startAt)}
              className={`rounded-lg border px-3 py-2 text-sm ${
                slotStart === slot.startAt ? "border-foreground bg-foreground text-background" : "border-border"
              }`}
            >
              {formatTime(new Date(slot.startAt))}
            </button>
          ))}
          {slots.length === 0 && (
            <div className="col-span-full rounded-lg bg-muted px-3 py-4 text-sm text-muted-foreground">
              <Trans>No open slots for this date.</Trans>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export function BookingFields({
  name,
  phone,
  email,
  note,
  onNameChange,
  onPhoneChange,
  onEmailChange,
  onNoteChange
}: {
  name: string;
  phone: string;
  email: string;
  note: string;
  onNameChange: (value: string) => void;
  onPhoneChange: (value: string) => void;
  onEmailChange: (value: string) => void;
  onNoteChange: (value: string) => void;
}) {
  return (
    <>
      <div className="grid grid-cols-3 gap-3">
        <TextInput label="Name" value={name} onChange={onNameChange} />
        <TextInput label="Phone" value={phone} onChange={onPhoneChange} />
        <TextInput label="Email" value={email} onChange={onEmailChange} />
      </div>
      <label className="text-sm font-medium" htmlFor="booking-note">
        <span className="mb-1 block">Anything we should know?</span>
        <textarea
          id="booking-note"
          value={note}
          onChange={(event) => onNoteChange(event.target.value)}
          className="min-h-24 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm"
        />
      </label>
    </>
  );
}

export function BookingFooter({
  selectedService,
  disabled,
  onSubmit
}: {
  selectedService?: PublicBookingService;
  disabled: boolean;
  onSubmit: () => void;
}) {
  return (
    <div className="flex items-center justify-between border-t border-border pt-4">
      <div className="text-sm text-muted-foreground">
        {selectedService?.depositCents ? (
          <span>Deposit required: {money(selectedService.depositCents)} via Paystack.</span>
        ) : (
          <Trans>No deposit required for this service.</Trans>
        )}
      </div>
      <Button onClick={onSubmit} disabled={disabled}>
        <Trans>Confirm request</Trans>
      </Button>
    </div>
  );
}

import { t } from "@lingui/core/macro";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";

import type { PublicEventType, PublicRescheduleBooking } from "./publicBookerTypes";

export function BookingFields({
  eventType,
  rescheduleBooking
}: Readonly<{ eventType: PublicEventType; rescheduleBooking: PublicRescheduleBooking | null }>) {
  const responses = rescheduleBooking?.responses ?? {};
  return (
    <div className="grid gap-4 sm:grid-cols-2" data-testid="public-booker-booking-fields">
      <TextField
        name="name"
        label={t`Name`}
        autoComplete="name"
        defaultValue={rescheduleBooking?.bookerName}
        required
      />
      <TextField
        name="email"
        label={t`Email`}
        type="email"
        autoComplete="email"
        defaultValue={rescheduleBooking?.bookerEmail}
        required
      />
      {(eventType.bookingFields ?? []).map((field) =>
        field.type === "textarea" ? (
          <TextAreaField
            key={field.name}
            name={field.name}
            label={field.label}
            required={field.required}
            defaultValue={responses[field.name]}
            className="sm:col-span-2"
          />
        ) : (
          <TextField
            key={field.name}
            name={field.name}
            label={field.label}
            required={field.required}
            defaultValue={responses[field.name]}
          />
        )
      )}
      <TextAreaField
        name="notes"
        label={t`Additional notes`}
        defaultValue={responses.notes}
        className="sm:col-span-2"
      />
    </div>
  );
}

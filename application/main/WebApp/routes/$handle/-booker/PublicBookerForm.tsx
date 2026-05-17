import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { CheckIcon, XIcon } from "lucide-react";

import { api } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../../-scheduling/ApiErrors";
import { formatMinutes } from "../../-scheduling/schedulingTypes";
import { formatLongDate, formatTime, type PublicEventType } from "./publicBookerTypes";

export function BookEventForm({
  handle,
  eventSlug,
  eventType,
  selectedSlot,
  selectedDuration,
  timezone,
  privateLink,
  onBack
}: Readonly<{
  handle: string;
  eventSlug: string;
  eventType: PublicEventType;
  selectedSlot: Date;
  selectedDuration: number;
  timezone: string;
  privateLink?: string;
  onBack: () => void;
}>) {
  const mutation = api.useMutation("post", "/api/public/bookings");
  return (
    <div
      className="fixed inset-0 z-40 flex bg-background lg:static lg:col-span-2 lg:border-t"
      data-testid="public-booker-form"
    >
      <div className="mx-auto flex w-full max-w-[42rem] flex-col gap-5 overflow-auto p-5 sm:p-6 lg:max-w-none lg:p-6">
        <div className="flex items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h2>{mutation.isSuccess ? <Trans>Booking confirmed</Trans> : <Trans>Enter your details</Trans>}</h2>
            <span className="text-sm text-muted-foreground">
              {`${formatLongDate(selectedSlot)} · ${formatTime(selectedSlot)} · ${formatMinutes(selectedDuration)}`}
            </span>
          </div>
          <Button type="button" variant="ghost" size="icon-sm" aria-label={t`Back to times`} onClick={onBack}>
            <XIcon />
          </Button>
        </div>
        {mutation.isSuccess ? (
          <BookingConfirmed />
        ) : (
          <BookEventFormFields
            eventType={eventType}
            error={mutation.error}
            isPending={mutation.isPending}
            onSubmit={(values) =>
              mutation.mutate({
                body: {
                  handle,
                  eventSlug,
                  startTime: selectedSlot.toISOString(),
                  duration: selectedDuration,
                  timeZone: timezone,
                  bookerName: values.bookerName,
                  bookerEmail: values.bookerEmail,
                  responses: values.responses,
                  privateLink: privateLink ?? null
                }
              })
            }
          />
        )}
      </div>
    </div>
  );
}

function BookingConfirmed() {
  return (
    <div className="flex flex-col items-start gap-3 rounded-md border bg-muted/20 p-4">
      <div className="flex size-10 items-center justify-center rounded-full bg-primary text-primary-foreground">
        <CheckIcon className="size-5" />
      </div>
      <span className="font-medium">
        <Trans>Your booking is confirmed.</Trans>
      </span>
      <span className="text-sm text-muted-foreground">
        <Trans>A confirmation will be sent to the email address you provided.</Trans>
      </span>
    </div>
  );
}

function BookEventFormFields({
  eventType,
  error,
  isPending,
  onSubmit
}: Readonly<{
  eventType: PublicEventType;
  error: Parameters<typeof GeneralApiErrors>[0]["error"];
  isPending: boolean;
  onSubmit: (values: { bookerName: string; bookerEmail: string; responses: Record<string, string> }) => void;
}>) {
  return (
    <Form
      className="gap-5"
      validationErrors={error?.errors}
      onSubmit={(event) => {
        event.preventDefault();
        const formData = new FormData(event.currentTarget);
        const responses = Object.fromEntries(
          (eventType.bookingFields ?? []).map((field) => [field.name, String(formData.get(field.name) ?? "")])
        );
        const notes = String(formData.get("notes") ?? "").trim();
        if (notes) responses.notes = notes;
        onSubmit({
          bookerName: String(formData.get("name") ?? ""),
          bookerEmail: String(formData.get("email") ?? ""),
          responses
        });
      }}
    >
      <GeneralApiErrors error={error} />
      <BookingFields eventType={eventType} />
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="submit" data-testid="public-booker-submit" isPending={isPending}>
          <Trans>Confirm booking</Trans>
        </Button>
      </div>
    </Form>
  );
}

function BookingFields({ eventType }: Readonly<{ eventType: PublicEventType }>) {
  return (
    <div className="grid gap-4 sm:grid-cols-2" data-testid="public-booker-booking-fields">
      <TextField name="name" label={t`Name`} autoComplete="name" required />
      <TextField name="email" label={t`Email`} type="email" autoComplete="email" required />
      {(eventType.bookingFields ?? []).map((field) =>
        field.type === "textarea" ? (
          <TextAreaField
            key={field.name}
            name={field.name}
            label={field.label}
            required={field.required}
            className="sm:col-span-2"
          />
        ) : (
          <TextField key={field.name} name={field.name} label={field.label} required={field.required} />
        )
      )}
      <TextAreaField name="notes" label={t`Additional notes`} className="sm:col-span-2" />
    </div>
  );
}

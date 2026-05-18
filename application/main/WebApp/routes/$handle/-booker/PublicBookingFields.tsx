import { t } from "@lingui/core/macro";
import { CheckboxField } from "@repo/ui/components/CheckboxField";
import { Label } from "@repo/ui/components/Label";
import { RadioGroupItem } from "@repo/ui/components/RadioGroup";
import { RadioGroupField } from "@repo/ui/components/RadioGroupField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";

import type { PublicEventType, PublicRescheduleBooking } from "./publicBookerTypes";

import { CheckboxOptionsField, MultiSelectBookingField, optionLabel, optionValue } from "./PublicBookingChoiceFields";

type BookingField = NonNullable<PublicEventType["bookingFields"]>[number];

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
      {(eventType.bookingFields ?? []).map((field) => (
        <PublicBookingField key={field.name} field={field} defaultValue={responses[field.name]} />
      ))}
      <TextAreaField
        name="notes"
        label={t`Additional notes`}
        defaultValue={responses.notes}
        className="sm:col-span-2"
      />
    </div>
  );
}

function PublicBookingField({ field, defaultValue }: Readonly<{ field: BookingField; defaultValue?: string }>) {
  const options = field.options ?? [];
  const readOnly = field.disableOnPrefill && hasPrefill(defaultValue);

  switch (field.type) {
    case "textarea":
      return (
        <TextAreaField
          name={field.name}
          label={field.label}
          required={field.required}
          defaultValue={defaultValue}
          readOnly={readOnly}
          className="sm:col-span-2"
        />
      );
    case "select":
      return (
        <SelectField
          name={field.name}
          label={field.label}
          required={field.required}
          items={options.map((option) => ({ value: optionValue(option), label: optionLabel(option) }))}
          defaultValue={defaultValue}
          readOnly={readOnly}
        >
          <SelectTrigger className="w-full">
            <SelectValue placeholder={t`Select an option`}>
              {(value: string) => options.find((option) => optionValue(option) === value)?.label ?? value}
            </SelectValue>
          </SelectTrigger>
          <SelectContent align="start">
            {options.map((option) => (
              <SelectItem key={optionValue(option)} value={optionValue(option)}>
                {optionLabel(option)}
              </SelectItem>
            ))}
          </SelectContent>
        </SelectField>
      );
    case "radio":
      return (
        <RadioGroupField
          name={field.name}
          label={field.label}
          required={field.required}
          defaultValue={defaultValue}
          readOnly={readOnly}
        >
          {options.map((option) => (
            <Label key={optionValue(option)} className="min-h-(--control-height) leading-snug">
              <RadioGroupItem value={optionValue(option)} />
              {optionLabel(option)}
            </Label>
          ))}
        </RadioGroupField>
      );
    case "checkbox":
      return <CheckboxOptionsField field={field} defaultValue={defaultValue} readOnly={readOnly} />;
    case "multiselect":
      return <MultiSelectBookingField field={field} defaultValue={defaultValue} readOnly={readOnly} />;
    case "boolean":
      return (
        <CheckboxField
          name={field.name}
          label={field.label}
          required={field.required}
          value="true"
          defaultChecked={defaultValue === "true"}
          readOnly={readOnly}
          className="sm:col-span-2"
        />
      );
    case "email":
    case "multiemail":
      return (
        <TextField
          name={field.name}
          label={field.label}
          type="email"
          required={field.required}
          defaultValue={defaultValue}
          readOnly={readOnly}
        />
      );
    case "phone":
      return (
        <TextField
          name={field.name}
          label={field.label}
          type="tel"
          required={field.required}
          defaultValue={defaultValue}
          readOnly={readOnly}
        />
      );
    case "url":
      return (
        <TextField
          name={field.name}
          label={field.label}
          type="url"
          required={field.required}
          defaultValue={defaultValue}
          readOnly={readOnly}
        />
      );
    case "number":
      return (
        <TextField
          name={field.name}
          label={field.label}
          type="number"
          required={field.required}
          defaultValue={defaultValue}
          readOnly={readOnly}
        />
      );
    default:
      return (
        <TextField
          name={field.name}
          label={field.label}
          required={field.required}
          defaultValue={defaultValue}
          readOnly={readOnly}
        />
      );
  }
}

function hasPrefill(value?: string) {
  return (value ?? "").trim().length > 0;
}

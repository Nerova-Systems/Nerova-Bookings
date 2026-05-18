import { t } from "@lingui/core/macro";
import { CheckboxField } from "@repo/ui/components/CheckboxField";
import { Field, FieldDescription, FieldError, FieldLabel } from "@repo/ui/components/Field";
import { Label } from "@repo/ui/components/Label";
import { MultiSelect } from "@repo/ui/components/MultiSelect";
import { RadioGroupItem } from "@repo/ui/components/RadioGroup";
import { RadioGroupField } from "@repo/ui/components/RadioGroupField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { useState } from "react";

import type { PublicEventType, PublicRescheduleBooking } from "./publicBookerTypes";

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

  switch (field.type) {
    case "textarea":
      return (
        <TextAreaField
          name={field.name}
          label={field.label}
          required={field.required}
          defaultValue={defaultValue}
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
        <RadioGroupField name={field.name} label={field.label} required={field.required} defaultValue={defaultValue}>
          {options.map((option) => (
            <Label key={optionValue(option)} className="min-h-(--control-height) leading-snug">
              <RadioGroupItem value={optionValue(option)} />
              {optionLabel(option)}
            </Label>
          ))}
        </RadioGroupField>
      );
    case "checkbox":
      return <CheckboxOptionsField field={field} defaultValue={defaultValue} />;
    case "multiselect":
      return <MultiSelectBookingField field={field} defaultValue={defaultValue} />;
    case "boolean":
      return (
        <CheckboxField
          name={field.name}
          label={field.label}
          required={field.required}
          value="true"
          defaultChecked={defaultValue === "true"}
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
        />
      );
    default:
      return <TextField name={field.name} label={field.label} required={field.required} defaultValue={defaultValue} />;
  }
}

function CheckboxOptionsField({ field, defaultValue }: Readonly<{ field: BookingField; defaultValue?: string }>) {
  const selectedValues = splitStoredValues(defaultValue);

  return (
    <Field className="flex flex-col sm:col-span-2">
      <FieldLabel>{field.label}</FieldLabel>
      <div className="grid gap-2 sm:grid-cols-2">
        {(field.options ?? []).map((option) => (
          <CheckboxField
            key={optionValue(option)}
            name={field.name}
            label={optionLabel(option)}
            value={optionValue(option)}
            defaultChecked={selectedValues.includes(optionValue(option))}
          />
        ))}
      </div>
      {field.required && <FieldDescription>{t`Required`}</FieldDescription>}
      <FieldError errors={[]} />
    </Field>
  );
}

function MultiSelectBookingField({ field, defaultValue }: Readonly<{ field: BookingField; defaultValue?: string }>) {
  const [value, setValue] = useState(() => splitStoredValues(defaultValue));

  return (
    <>
      <MultiSelect
        name={`${field.name}-input`}
        label={field.label}
        placeholder={t`Select options`}
        items={(field.options ?? []).map((option) => ({ id: optionValue(option), label: optionLabel(option) }))}
        value={value}
        onChange={setValue}
        className="sm:col-span-2"
      />
      <input type="hidden" name={field.name} value={value.join(",")} />
    </>
  );
}

function optionValue(option: BookingField["options"][number]) {
  return option.value;
}

function optionLabel(option: BookingField["options"][number]) {
  return option.label || option.value;
}

function splitStoredValues(value?: string) {
  return (value ?? "")
    .split(",")
    .map((part) => part.trim())
    .filter(Boolean);
}

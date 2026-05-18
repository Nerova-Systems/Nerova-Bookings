import { t } from "@lingui/core/macro";
import { CheckboxField } from "@repo/ui/components/CheckboxField";
import { Field, FieldDescription, FieldError, FieldLabel } from "@repo/ui/components/Field";
import { MultiSelect } from "@repo/ui/components/MultiSelect";
import { useState } from "react";

import type { PublicEventType } from "./publicBookerTypes";

type BookingField = NonNullable<PublicEventType["bookingFields"]>[number];

export function CheckboxOptionsField({
  field,
  defaultValue,
  readOnly
}: Readonly<{ field: BookingField; defaultValue?: string; readOnly: boolean }>) {
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
            readOnly={readOnly}
          />
        ))}
      </div>
      {field.required && <FieldDescription>{t`Required`}</FieldDescription>}
      <FieldError errors={[]} />
    </Field>
  );
}

export function MultiSelectBookingField({
  field,
  defaultValue,
  readOnly
}: Readonly<{ field: BookingField; defaultValue?: string; readOnly: boolean }>) {
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
        readOnly={readOnly}
        className="sm:col-span-2"
      />
      <input type="hidden" name={field.name} value={value.join(",")} />
    </>
  );
}

export function optionValue(option: BookingField["options"][number]) {
  return option.value;
}

export function optionLabel(option: BookingField["options"][number]) {
  return option.label || option.value;
}

function splitStoredValues(value?: string) {
  return (value ?? "")
    .split(",")
    .map((part) => part.trim())
    .filter(Boolean);
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Checkbox } from "@repo/ui/components/Checkbox";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { Switch } from "@repo/ui/components/Switch";

import { EMPTY_SELECT_VALUE, parseMultiChoice, type EditableField } from "./verticalFieldHelpers";
import { localizeOption } from "./verticalFieldOptionLocalization";

type InputProps = Readonly<{
  field: EditableField;
  value: string;
  isPending: boolean;
  onChange: (value: string) => void;
  onSave: (value: string) => void;
}>;

export function BooleanFieldInput({ value, isPending, label, onChange, onSave }: InputProps & { label: string }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="text-sm text-muted-foreground">{value ? (value === "true" ? t`Yes` : t`No`) : t`Add`}</span>
      <Switch
        checked={value === "true"}
        disabled={isPending}
        aria-label={label}
        onCheckedChange={(checked) => {
          const nextValue = String(checked);
          onChange(nextValue);
          onSave(nextValue);
        }}
      />
    </div>
  );
}

export function ChoiceFieldInput({
  field,
  value,
  isPending,
  placeholder,
  onChange,
  onSave
}: InputProps & { placeholder: string }) {
  return (
    <SelectField
      name={field.key}
      value={value || EMPTY_SELECT_VALUE}
      disabled={isPending}
      onValueChange={(nextValue) => {
        const valueToSave = nextValue === EMPTY_SELECT_VALUE ? "" : String(nextValue);
        onChange(valueToSave);
        onSave(valueToSave);
      }}
    >
      <SelectTrigger className="w-full">
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={EMPTY_SELECT_VALUE}>
          <Trans>Add later</Trans>
        </SelectItem>
        {field.options.map((option) => (
          <SelectItem key={option} value={option}>
            {localizeOption(option)}
          </SelectItem>
        ))}
      </SelectContent>
    </SelectField>
  );
}

export function MultiChoiceFieldInput({ field, value, isPending, onChange, onSave }: InputProps) {
  return (
    <div className="flex flex-wrap gap-2">
      {field.options.map((option) => {
        const selectedValues = parseMultiChoice(value);
        const isSelected = selectedValues.includes(option);
        return (
          <label
            key={option}
            className="inline-flex items-center gap-2 rounded-md border px-2 py-1.5 text-sm hover:bg-muted/60"
          >
            <Checkbox
              checked={isSelected}
              disabled={isPending}
              onCheckedChange={(checked) => {
                const nextValues = checked
                  ? [...selectedValues, option]
                  : selectedValues.filter((selected) => selected !== option);
                const nextValue = nextValues.join(", ");
                onChange(nextValue);
                onSave(nextValue);
              }}
            />
            {localizeOption(option)}
          </label>
        );
      })}
    </div>
  );
}

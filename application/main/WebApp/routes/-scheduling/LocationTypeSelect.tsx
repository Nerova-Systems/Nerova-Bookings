import { t } from "@lingui/core/macro";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";

export function LocationTypeSelect({
  value,
  onChange
}: Readonly<{ value: string | null | undefined; onChange: (locationType: string) => void }>) {
  const locationTypes = [
    { value: "link", label: t`Link` },
    { value: "phone", label: t`Phone` },
    { value: "in-person", label: t`In person` }
  ];

  return (
    <SelectField
      name="locationType"
      label={t`Location type`}
      items={locationTypes}
      value={value || "link"}
      onValueChange={(locationType) => onChange(locationType ?? "link")}
    >
      <SelectTrigger>
        <SelectValue>
          {(locationType: string) => locationTypes.find((item) => item.value === locationType)?.label}
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {locationTypes.map((locationType) => (
          <SelectItem key={locationType.value} value={locationType.value}>
            {locationType.label}
          </SelectItem>
        ))}
      </SelectContent>
    </SelectField>
  );
}

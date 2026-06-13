import { t } from "@lingui/core/macro";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";

import { AppCategory, api } from "@/shared/lib/api/client";

export function LocationTypeSelect({
  value,
  onChange
}: Readonly<{ value: string | null | undefined; onChange: (locationType: string) => void }>) {
  // Installed conferencing connectors (Google Meet, MS Teams, Zoom, ...) appear as additional
  // location choices once the user has connected them on /apps/installed. The location string
  // stored on the service matches the app slug so back-end booking handlers can resolve the
  // matching IConferenceLinkProvider when the meeting is created.
  const { data: appsData } = api.useQuery("get", "/api/apps");
  const conferencingApps = (appsData?.apps ?? []).filter(
    (app) => app.category === AppCategory.Conferencing && app.isConnectedForUser
  );

  const locationTypes = [
    { value: "link", label: t`Video or online` },
    { value: "phone", label: t`Phone` },
    { value: "in-person", label: t`In person` },
    ...conferencingApps.map((app) => ({ value: app.slug, label: app.name }))
  ];

  return (
    <SelectField
      name="locationType"
      label={t`Where it happens`}
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

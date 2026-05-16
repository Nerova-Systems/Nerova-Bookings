import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { TimeZonePicker } from "@repo/ui/components/TimeZonePicker";
import { InfoIcon, PlusIcon } from "lucide-react";

import type { SchedulePayload } from "./schedulingTypes";

export function DateOverridesCard() {
  return (
    <div className="rounded-md border p-6">
      <div className="flex items-center gap-1">
        <h3 className="text-base font-semibold">
          <Trans>Date overrides</Trans>
        </h3>
        <InfoIcon className="size-4 text-muted-foreground" />
      </div>
      <p className="mt-1 text-sm text-muted-foreground">
        <Trans>Add dates when your availability changes from your daily hours.</Trans>
      </p>
      <Button type="button" variant="outline" className="mt-5">
        <PlusIcon />
        <Trans>Add an override</Trans>
      </Button>
    </div>
  );
}

export function AvailabilitySidePanel({
  value,
  onChange
}: Readonly<{ value: SchedulePayload; onChange: (value: SchedulePayload) => void }>) {
  return (
    <aside className="flex flex-col gap-8">
      <TimeZonePicker
        name="timeZone"
        label={t`Timezone`}
        value={value.timeZone}
        onValueChange={(timeZone) => onChange({ ...value, timeZone: timeZone ?? "UTC" })}
      />
      <div className="border-t pt-8">
        <div className="rounded-md border p-5">
          <h3 className="text-base font-semibold">
            <Trans>Something doesn't look right?</Trans>
          </h3>
          <Button type="button" variant="outline" className="mt-4">
            <Trans>Launch troubleshooter</Trans>
          </Button>
        </div>
      </div>
    </aside>
  );
}

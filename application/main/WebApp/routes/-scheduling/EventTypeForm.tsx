import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { SaveIcon } from "lucide-react";

import type { ApiValidationError, EventTypePayload, Schedule } from "./schedulingTypes";

import { GeneralApiErrors } from "./ApiErrors";
import { slugify } from "./schedulingTypes";

export function EventTypeForm({
  value,
  schedules,
  onChange,
  onSubmit,
  error,
  isPending,
  submitLabel
}: Readonly<{
  value: EventTypePayload;
  schedules: Schedule[];
  onChange: (value: EventTypePayload) => void;
  onSubmit: (value: EventTypePayload) => void;
  error?: ApiValidationError;
  isPending?: boolean;
  submitLabel: string;
}>) {
  return (
    <Form
      validationBehavior="aria"
      validationErrors={error?.errors}
      className="gap-5"
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit(value);
      }}
    >
      <GeneralApiErrors error={error} />
      <div className="grid gap-4 md:grid-cols-2">
        <TextField
          name="title"
          label={t`Title`}
          required={true}
          value={value.title}
          onChange={(title) => onChange({ ...value, title, slug: value.slug || slugify(title) })}
        />
        <TextField
          name="slug"
          label={t`Slug`}
          required={true}
          value={value.slug}
          onChange={(slug) => onChange({ ...value, slug: slugify(slug) })}
        />
      </div>
      <TextAreaField
        name="description"
        label={t`Description`}
        lines={3}
        value={value.description ?? ""}
        onChange={(description) => onChange({ ...value, description: description || null })}
      />
      <div className="grid gap-4 md:grid-cols-4">
        <NumberField
          name="durationMinutes"
          label={t`Duration`}
          minValue={5}
          maxValue={1440}
          value={value.durationMinutes}
          onChange={(durationMinutes) => onChange({ ...value, durationMinutes: durationMinutes ?? 30 })}
        />
        <NumberField
          name="slotIntervalMinutes"
          label={t`Slot interval`}
          minValue={5}
          maxValue={1440}
          value={value.slotIntervalMinutes}
          onChange={(slotIntervalMinutes) => onChange({ ...value, slotIntervalMinutes: slotIntervalMinutes ?? 30 })}
        />
        <NumberField
          name="minimumBookingNoticeMinutes"
          label={t`Notice`}
          minValue={0}
          maxValue={525600}
          value={value.minimumBookingNoticeMinutes}
          onChange={(minimumBookingNoticeMinutes) =>
            onChange({ ...value, minimumBookingNoticeMinutes: minimumBookingNoticeMinutes ?? 0 })
          }
        />
        <SwitchField
          name="hidden"
          label={t`Hidden`}
          alignWithLabel={true}
          checked={value.hidden}
          onCheckedChange={(hidden) => onChange({ ...value, hidden })}
        />
      </div>
      <div className="grid gap-4 md:grid-cols-3">
        <TextField
          name="beforeEventBufferMinutes"
          label={t`Before buffer`}
          type="number"
          value={String(value.beforeEventBufferMinutes)}
          onChange={(beforeEventBufferMinutes) =>
            onChange({ ...value, beforeEventBufferMinutes: Number(beforeEventBufferMinutes) || 0 })
          }
        />
        <TextField
          name="afterEventBufferMinutes"
          label={t`After buffer`}
          type="number"
          value={String(value.afterEventBufferMinutes)}
          onChange={(afterEventBufferMinutes) =>
            onChange({ ...value, afterEventBufferMinutes: Number(afterEventBufferMinutes) || 0 })
          }
        />
        <label className="flex flex-col gap-2 text-sm font-medium">
          <span>
            <Trans>Schedule</Trans>
          </span>
          <select
            name="scheduleId"
            value={value.scheduleId}
            onChange={(event) => onChange({ ...value, scheduleId: event.target.value })}
            className="h-[var(--control-height)] rounded-md border border-input bg-input/30 px-3 text-sm"
          >
            {schedules.map((schedule) => (
              <option key={schedule.id} value={schedule.id}>
                {schedule.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <TextField
          name="locationType"
          label={t`Location type`}
          value={value.locationType ?? ""}
          onChange={(locationType) => onChange({ ...value, locationType: locationType || null })}
        />
        <TextField
          name="locationValue"
          label={t`Location`}
          value={value.locationValue ?? ""}
          onChange={(locationValue) => onChange({ ...value, locationValue: locationValue || null })}
        />
      </div>
      <div className="flex justify-end">
        <Button type="submit" isPending={isPending} disabled={schedules.length === 0}>
          <SaveIcon />
          {isPending ? <Trans>Saving...</Trans> : submitLabel}
        </Button>
      </div>
    </Form>
  );
}

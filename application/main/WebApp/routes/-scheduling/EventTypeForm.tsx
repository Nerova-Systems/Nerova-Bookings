import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { SaveIcon } from "lucide-react";

import type { ApiValidationError, EventTypePayload, Schedule } from "./schedulingTypes";

import { GeneralApiErrors } from "./ApiErrors";
import { LocationTypeSelect } from "./LocationTypeSelect";
import { isEventTypePayloadSubmittable, slugify } from "./schedulingTypes";

function ScheduleSelect({
  value,
  schedules,
  onChange
}: Readonly<{ value: string; schedules: Schedule[]; onChange: (scheduleId: string) => void }>) {
  const scheduleItems = schedules.map((schedule) => ({ value: schedule.id, label: schedule.name }));

  return (
    <SelectField
      name="scheduleId"
      label={t`Schedule`}
      items={scheduleItems}
      value={value}
      onValueChange={(scheduleId) => onChange(scheduleId ?? "")}
    >
      <SelectTrigger>
        <SelectValue placeholder={t`Select schedule`}>
          {(scheduleId: string) => schedules.find((schedule) => schedule.id === scheduleId)?.name ?? t`Select schedule`}
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {schedules.map((schedule) => (
          <SelectItem key={schedule.id} value={schedule.id}>
            {schedule.name}
          </SelectItem>
        ))}
      </SelectContent>
    </SelectField>
  );
}

export function EventTypeForm({
  value,
  schedules,
  onChange,
  onSubmit,
  error,
  isPending,
  submitLabel,
  formId,
  showSubmit = true
}: Readonly<{
  value: EventTypePayload;
  schedules: Schedule[];
  onChange: (value: EventTypePayload) => void;
  onSubmit: (value: EventTypePayload) => void;
  error?: ApiValidationError;
  isPending?: boolean;
  submitLabel: string;
  formId?: string;
  showSubmit?: boolean;
}>) {
  const canSubmit = schedules.length > 0 && isEventTypePayloadSubmittable(value);

  return (
    <Form
      id={formId}
      validationBehavior="aria"
      validationErrors={error?.errors}
      className="gap-5"
      onSubmit={(event) => {
        event.preventDefault();
        if (!canSubmit) return;
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
        <NumberField
          name="beforeEventBufferMinutes"
          label={t`Before buffer`}
          minValue={0}
          maxValue={1440}
          value={value.beforeEventBufferMinutes}
          onChange={(beforeEventBufferMinutes) =>
            onChange({ ...value, beforeEventBufferMinutes: beforeEventBufferMinutes ?? 0 })
          }
        />
        <NumberField
          name="afterEventBufferMinutes"
          label={t`After buffer`}
          minValue={0}
          maxValue={1440}
          value={value.afterEventBufferMinutes}
          onChange={(afterEventBufferMinutes) =>
            onChange({ ...value, afterEventBufferMinutes: afterEventBufferMinutes ?? 0 })
          }
        />
        <ScheduleSelect
          value={value.scheduleId}
          schedules={schedules}
          onChange={(scheduleId) => onChange({ ...value, scheduleId })}
        />
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <LocationTypeSelect
          value={value.locationType ?? ""}
          onChange={(locationType) => onChange({ ...value, locationType })}
        />
        <TextField
          name="locationValue"
          label={t`Location`}
          value={value.locationValue ?? ""}
          onChange={(locationValue) => onChange({ ...value, locationValue: locationValue || null })}
        />
      </div>
      {showSubmit && (
        <div className="flex justify-end">
          <Button type="submit" isPending={isPending} disabled={!canSubmit}>
            <SaveIcon />
            {isPending ? <Trans>Saving...</Trans> : submitLabel}
          </Button>
        </div>
      )}
    </Form>
  );
}

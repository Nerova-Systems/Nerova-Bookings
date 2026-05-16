import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { CheckboxField } from "@repo/ui/components/CheckboxField";
import { Form } from "@repo/ui/components/Form";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextField } from "@repo/ui/components/TextField";
import { TimeZonePicker } from "@repo/ui/components/TimeZonePicker";
import { ClockIcon, PlusIcon, SaveIcon, Trash2Icon } from "lucide-react";

import type { ApiValidationError, AvailabilityWindow, SchedulePayload } from "./schedulingTypes";

import { GeneralApiErrors } from "./ApiErrors";
import { formatMinutes, isSchedulePayloadSubmittable, parseTime } from "./schedulingTypes";

function WindowEditor({
  windows,
  onChange
}: Readonly<{ windows: AvailabilityWindow[]; onChange: (windows: AvailabilityWindow[]) => void }>) {
  const weekDays = [
    { value: 0, label: t`Sunday` },
    { value: 1, label: t`Monday` },
    { value: 2, label: t`Tuesday` },
    { value: 3, label: t`Wednesday` },
    { value: 4, label: t`Thursday` },
    { value: 5, label: t`Friday` },
    { value: 6, label: t`Saturday` }
  ];

  const updateWindow = (index: number, next: AvailabilityWindow) => {
    onChange(windows.map((window, currentIndex) => (currentIndex === index ? next : window)));
  };

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-3">
        <h3 className="text-base font-medium">
          <Trans>Weekly availability</Trans>
        </h3>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => onChange([...windows, { days: [1], startMinute: 540, endMinute: 1020 }])}
        >
          <PlusIcon />
          <Trans>Add window</Trans>
        </Button>
      </div>
      {windows.map((window, index) => (
        <div key={index} className="grid gap-3 rounded-md border p-3 md:grid-cols-[1fr_9rem_9rem_auto]">
          <div className="grid grid-cols-2 gap-x-4 gap-y-1 sm:grid-cols-4">
            {weekDays.map((day) => (
              <CheckboxField
                key={day.value}
                name={`availabilityWindows.${index}.days`}
                label={day.label}
                checked={window.days.includes(day.value)}
                onCheckedChange={(checked) => {
                  const days = checked
                    ? [...window.days, day.value].sort()
                    : window.days.filter((value) => value !== day.value);
                  updateWindow(index, { ...window, days });
                }}
              />
            ))}
          </div>
          <TextField
            name={`availabilityWindows.${index}.startMinute`}
            label={t`Start`}
            value={formatMinutes(window.startMinute)}
            startIcon={<ClockIcon />}
            onChange={(value) => updateWindow(index, { ...window, startMinute: parseTime(value, window.startMinute) })}
          />
          <TextField
            name={`availabilityWindows.${index}.endMinute`}
            label={t`End`}
            value={formatMinutes(window.endMinute)}
            startIcon={<ClockIcon />}
            onChange={(value) => updateWindow(index, { ...window, endMinute: parseTime(value, window.endMinute) })}
          />
          <div className="flex items-end">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => onChange(windows.filter((_, currentIndex) => currentIndex !== index))}
              disabled={windows.length === 1}
              aria-label={t`Remove window`}
            >
              <Trash2Icon />
              <Trans>Remove</Trans>
            </Button>
          </div>
        </div>
      ))}
    </div>
  );
}

export function ScheduleForm({
  value,
  onChange,
  onSubmit,
  error,
  isPending,
  submitLabel
}: Readonly<{
  value: SchedulePayload;
  onChange: (value: SchedulePayload) => void;
  onSubmit: (value: SchedulePayload) => void;
  error?: ApiValidationError;
  isPending?: boolean;
  submitLabel: string;
}>) {
  const canSubmit = isSchedulePayloadSubmittable(value);

  return (
    <Form
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
          name="name"
          label={t`Name`}
          required={true}
          value={value.name}
          onChange={(name) => onChange({ ...value, name })}
        />
        <TimeZonePicker
          name="timeZone"
          label={t`Time zone`}
          value={value.timeZone}
          onValueChange={(timeZone) => onChange({ ...value, timeZone: timeZone ?? "UTC" })}
        />
      </div>
      <SwitchField
        name="isDefault"
        label={t`Default schedule`}
        checked={value.isDefault}
        onCheckedChange={(isDefault) => onChange({ ...value, isDefault })}
      />
      <WindowEditor
        windows={value.availabilityWindows}
        onChange={(availabilityWindows) => onChange({ ...value, availabilityWindows })}
      />
      <div className="flex justify-end">
        <Button type="submit" isPending={isPending} disabled={!canSubmit}>
          <SaveIcon />
          {isPending ? <Trans>Saving...</Trans> : submitLabel}
        </Button>
      </div>
    </Form>
  );
}

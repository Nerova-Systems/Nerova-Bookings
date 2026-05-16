import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { TextField } from "@repo/ui/components/TextField";
import { SaveIcon } from "lucide-react";

import type { ApiValidationError, SchedulePayload } from "./schedulingTypes";

import { GeneralApiErrors } from "./ApiErrors";
import { WindowEditor } from "./AvailabilityEditor";
import { AvailabilitySidePanel, DateOverridesCard } from "./AvailabilitySidePanel";
import { isSchedulePayloadSubmittable } from "./schedulingTypes";

function ScheduleNameField({
  value,
  onChange
}: Readonly<{ value: SchedulePayload; onChange: (value: SchedulePayload) => void }>) {
  return (
    <div className="sr-only">
      <TextField
        name="name"
        label={t`Name`}
        required={true}
        value={value.name}
        onChange={(name) => onChange({ ...value, name })}
      />
    </div>
  );
}

function SubmitButton({
  isPending,
  canSubmit,
  submitLabel
}: Readonly<{ isPending?: boolean; canSubmit: boolean; submitLabel: string }>) {
  return (
    <div className="flex justify-end">
      <Button type="submit" isPending={isPending} disabled={!canSubmit}>
        <SaveIcon />
        {isPending ? <Trans>Saving...</Trans> : submitLabel}
      </Button>
    </div>
  );
}

export function ScheduleForm({
  value,
  onChange,
  onSubmit,
  error,
  isPending,
  submitLabel,
  canUnsetDefault = true,
  showSubmit = true
}: Readonly<{
  value: SchedulePayload;
  onChange: (value: SchedulePayload) => void;
  onSubmit: (value: SchedulePayload) => void;
  error?: ApiValidationError;
  isPending?: boolean;
  submitLabel: string;
  canUnsetDefault?: boolean;
  showSubmit?: boolean;
}>) {
  const canSubmit = isSchedulePayloadSubmittable(value);

  return (
    <Form
      id="availability-form"
      validationBehavior="aria"
      validationErrors={error?.errors}
      className="gap-6"
      onSubmit={(event) => {
        event.preventDefault();
        if (!canSubmit) return;
        onSubmit(value);
      }}
    >
      <GeneralApiErrors error={error} />
      <ScheduleNameField value={value} onChange={onChange} />
      <div className="grid gap-8 xl:grid-cols-[minmax(0,1fr)_22rem]">
        <div className="flex min-w-0 flex-col gap-6">
          <WindowEditor
            windows={value.availabilityWindows}
            onChange={(availabilityWindows) => onChange({ ...value, availabilityWindows })}
          />
          <DateOverridesCard />
          {showSubmit && <SubmitButton isPending={isPending} canSubmit={canSubmit} submitLabel={submitLabel} />}
        </div>
        <AvailabilitySidePanel value={value} onChange={onChange} />
      </div>
      <input type="hidden" name="isDefault" value={String(value.isDefault)} />
      {!canUnsetDefault && <input type="hidden" name="defaultLocked" value="true" />}
    </Form>
  );
}

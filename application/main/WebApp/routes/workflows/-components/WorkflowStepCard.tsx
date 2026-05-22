import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { SaveIcon, Trash2Icon } from "lucide-react";

import type { WorkflowTrigger } from "@/shared/lib/api/client";

import { WorkflowReminderTemplate, WorkflowTimeUnit } from "@/shared/lib/api/client";

import type { ApiValidationError, WorkflowStepDraft } from "./workflowTypes";

import { WorkflowApiErrors } from "./WorkflowApiErrors";
import { WorkflowStepActionTemplateRow } from "./WorkflowStepActionTemplateRow";
import {
  actionRequiresSendTo,
  getWorkflowTimeUnitLabel,
  isEmailAction,
  triggerSupportsDelay,
  WORKFLOW_TIME_UNIT_ORDER
} from "./workflowTypes";

interface WorkflowStepCardProps {
  index: number;
  trigger: WorkflowTrigger;
  draft: WorkflowStepDraft;
  isDirty: boolean;
  isPending: boolean;
  isRemoving: boolean;
  error: ApiValidationError;
  onChange: (draft: WorkflowStepDraft) => void;
  onSave: () => void;
  onRemove: () => void;
}

export function WorkflowStepCard({
  index,
  trigger,
  draft,
  isDirty,
  isPending,
  isRemoving,
  error,
  onChange,
  onSave,
  onRemove
}: Readonly<WorkflowStepCardProps>) {
  const showDelay = triggerSupportsDelay(trigger);
  const showSendTo = actionRequiresSendTo(draft.action);
  const showEmailFields = isEmailAction(draft.action);
  const showCustomTemplateBody = draft.template === WorkflowReminderTemplate.Custom;
  const stepNumber = index + 1;
  const isNewStep = draft.id === null;

  const timeUnitItems = WORKFLOW_TIME_UNIT_ORDER.map((value) => ({ value, label: getWorkflowTimeUnitLabel(value) }));

  return (
    <Card>
      <CardHeader className="border-b">
        <CardTitle className="flex items-center gap-2">
          <span className="inline-flex size-6 items-center justify-center rounded-full bg-muted text-xs font-semibold">
            {stepNumber}
          </span>
          <span>{isNewStep ? <Trans>New step</Trans> : <Trans>Step {stepNumber}</Trans>}</span>
        </CardTitle>
        {/* TODO: backend has no reorder API yet — steps render in insertion order without manual reordering. */}
      </CardHeader>
      <CardContent className="flex flex-col gap-4 pt-6">
        <WorkflowApiErrors error={error} />
        <WorkflowStepActionTemplateRow stepNumber={stepNumber} draft={draft} onChange={onChange} />
        {showSendTo && (
          <TextField
            name={`step-${stepNumber}-sendTo`}
            label={t`Send to`}
            description={t`Email address or phone number depending on the action.`}
            value={draft.sendTo ?? ""}
            onChange={(value) => onChange({ ...draft, sendTo: value })}
          />
        )}
        {showDelay && (
          <div className="grid gap-4 sm:grid-cols-2">
            <NumberField
              name={`step-${stepNumber}-reminderTime`}
              label={t`Time before/after event`}
              minValue={0}
              step={1}
              allowEmpty={true}
              value={draft.reminderTime ?? ""}
              onChange={(value) => onChange({ ...draft, reminderTime: value })}
            />
            <SelectField<WorkflowTimeUnit>
              name={`step-${stepNumber}-timeUnit`}
              label={t`Unit`}
              items={timeUnitItems}
              value={draft.timeUnit ?? WorkflowTimeUnit.Hours}
              onValueChange={(value) => value !== null && onChange({ ...draft, timeUnit: value })}
            >
              <SelectTrigger>
                <SelectValue>
                  {(value: WorkflowTimeUnit) => timeUnitItems.find((item) => item.value === value)?.label}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {timeUnitItems.map((item) => (
                  <SelectItem key={item.value} value={item.value}>
                    {item.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </SelectField>
          </div>
        )}
        {showEmailFields && (
          <TextField
            name={`step-${stepNumber}-emailSubject`}
            label={t`Email subject`}
            value={draft.emailSubject ?? ""}
            onChange={(value) => onChange({ ...draft, emailSubject: value })}
          />
        )}
        {showCustomTemplateBody && (
          <TextAreaField
            name={`step-${stepNumber}-emailBody`}
            label={t`Message`}
            description={t`Variables like {EVENT_NAME} and {ATTENDEE_NAME} are replaced at send time.`}
            lines={5}
            value={draft.emailBody ?? ""}
            onChange={(value) => onChange({ ...draft, emailBody: value })}
          />
        )}
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onRemove} isPending={isRemoving} disabled={isPending}>
            <Trash2Icon />
            <Trans>Remove</Trans>
          </Button>
          <Button onClick={onSave} disabled={!isDirty || isRemoving} isPending={isPending}>
            <SaveIcon />
            {isNewStep ? <Trans>Add step</Trans> : <Trans>Save</Trans>}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

import { t } from "@lingui/core/macro";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";

import type { WorkflowAction, WorkflowReminderTemplate } from "@/shared/lib/api/client";

import type { WorkflowStepDraft } from "./workflowTypes";

import {
  actionRequiresSendTo,
  getWorkflowActionLabel,
  getWorkflowTemplateLabel,
  isEmailAction,
  WORKFLOW_ACTION_ORDER,
  WORKFLOW_TEMPLATE_ORDER
} from "./workflowTypes";

export function WorkflowStepActionTemplateRow({
  stepNumber,
  draft,
  onChange
}: Readonly<{
  stepNumber: number;
  draft: WorkflowStepDraft;
  onChange: (draft: WorkflowStepDraft) => void;
}>) {
  const actionItems = WORKFLOW_ACTION_ORDER.map((value) => ({ value, label: getWorkflowActionLabel(value) }));
  const templateItems = WORKFLOW_TEMPLATE_ORDER.map((value) => ({ value, label: getWorkflowTemplateLabel(value) }));
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <SelectField<WorkflowAction>
        name={`step-${stepNumber}-action`}
        label={t`Action`}
        items={actionItems}
        value={draft.action}
        onValueChange={(value) => {
          if (value === null) return;
          onChange({
            ...draft,
            action: value,
            sendTo: actionRequiresSendTo(value) ? draft.sendTo : null,
            emailSubject: isEmailAction(value) ? draft.emailSubject : null
          });
        }}
      >
        <SelectTrigger>
          <SelectValue>
            {(value: WorkflowAction) => actionItems.find((item) => item.value === value)?.label}
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          {actionItems.map((item) => (
            <SelectItem key={item.value} value={item.value}>
              {item.label}
            </SelectItem>
          ))}
        </SelectContent>
      </SelectField>
      <SelectField<WorkflowReminderTemplate>
        name={`step-${stepNumber}-template`}
        label={t`Template`}
        items={templateItems}
        value={draft.template}
        onValueChange={(value) => value !== null && onChange({ ...draft, template: value })}
      >
        <SelectTrigger>
          <SelectValue>
            {(value: WorkflowReminderTemplate) => templateItems.find((item) => item.value === value)?.label}
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          {templateItems.map((item) => (
            <SelectItem key={item.value} value={item.value}>
              {item.label}
            </SelectItem>
          ))}
        </SelectContent>
      </SelectField>
    </div>
  );
}

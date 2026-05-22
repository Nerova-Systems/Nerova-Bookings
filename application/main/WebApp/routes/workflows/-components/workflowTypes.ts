import { t } from "@lingui/core/macro";

import {
  type Schemas,
  WorkflowAction,
  WorkflowReminderTemplate,
  WorkflowTimeUnit,
  WorkflowTrigger
} from "@/shared/lib/api/client";

export type Workflow = Schemas["WorkflowResponse"];
export type WorkflowStep = Schemas["WorkflowStepResponse"];
export type EventType = Schemas["EventTypeResponse"];
export type ApiValidationError = Schemas["HttpValidationProblemDetails"] | null | undefined;

export function getApiErrorMessages(error: ApiValidationError): string[] {
  return [error?.detail, ...Object.values(error?.errors ?? {}).flat()].filter(
    (value): value is string => typeof value === "string" && value.length > 0
  );
}

/**
 * Triggers that schedule a reminder relative to the event start/end. Other triggers fire
 * immediately when their booking lifecycle event happens, so the step does not need a delay.
 */
const DELAYED_TRIGGERS: ReadonlySet<WorkflowTrigger> = new Set([
  WorkflowTrigger.BeforeEvent,
  WorkflowTrigger.AfterEvent
]);

export function triggerSupportsDelay(trigger: WorkflowTrigger): boolean {
  return DELAYED_TRIGGERS.has(trigger);
}

export function getWorkflowTriggerLabel(trigger: WorkflowTrigger): string {
  switch (trigger) {
    case WorkflowTrigger.NewEvent:
      return t`When a new booking is created`;
    case WorkflowTrigger.BeforeEvent:
      return t`Before the event starts`;
    case WorkflowTrigger.AfterEvent:
      return t`After the event ends`;
    case WorkflowTrigger.EventCancelled:
      return t`When a booking is cancelled`;
    case WorkflowTrigger.RescheduleEvent:
      return t`When a booking is rescheduled`;
  }
}

export function getWorkflowActionLabel(action: WorkflowAction): string {
  switch (action) {
    case WorkflowAction.EmailHost:
      return t`Email to host`;
    case WorkflowAction.EmailAttendee:
      return t`Email to attendee`;
    case WorkflowAction.EmailAddress:
      return t`Email to a specific address`;
    case WorkflowAction.SmsAttendee:
      return t`SMS to attendee`;
    case WorkflowAction.SmsNumber:
      return t`SMS to a specific number`;
    case WorkflowAction.WhatsappAttendee:
      return t`WhatsApp to attendee`;
    case WorkflowAction.WhatsappNumber:
      return t`WhatsApp to a specific number`;
  }
}

export function getWorkflowTimeUnitLabel(unit: WorkflowTimeUnit): string {
  switch (unit) {
    case WorkflowTimeUnit.Minutes:
      return t`Minutes`;
    case WorkflowTimeUnit.Hours:
      return t`Hours`;
    case WorkflowTimeUnit.Days:
      return t`Days`;
    case WorkflowTimeUnit.Weeks:
      return t`Weeks`;
  }
}

export function getWorkflowTemplateLabel(template: WorkflowReminderTemplate): string {
  switch (template) {
    case WorkflowReminderTemplate.Reminder:
      return t`Reminder`;
    case WorkflowReminderTemplate.Custom:
      return t`Custom`;
    case WorkflowReminderTemplate.RatingRequest:
      return t`Rating request`;
    case WorkflowReminderTemplate.ThankYou:
      return t`Thank you`;
  }
}

export const WORKFLOW_TRIGGER_ORDER: readonly WorkflowTrigger[] = [
  WorkflowTrigger.NewEvent,
  WorkflowTrigger.BeforeEvent,
  WorkflowTrigger.AfterEvent,
  WorkflowTrigger.EventCancelled,
  WorkflowTrigger.RescheduleEvent
];

export const WORKFLOW_ACTION_ORDER: readonly WorkflowAction[] = [
  WorkflowAction.EmailHost,
  WorkflowAction.EmailAttendee,
  WorkflowAction.EmailAddress,
  WorkflowAction.SmsAttendee,
  WorkflowAction.SmsNumber,
  WorkflowAction.WhatsappAttendee,
  WorkflowAction.WhatsappNumber
];

export const WORKFLOW_TIME_UNIT_ORDER: readonly WorkflowTimeUnit[] = [
  WorkflowTimeUnit.Minutes,
  WorkflowTimeUnit.Hours,
  WorkflowTimeUnit.Days,
  WorkflowTimeUnit.Weeks
];

export const WORKFLOW_TEMPLATE_ORDER: readonly WorkflowReminderTemplate[] = [
  WorkflowReminderTemplate.Reminder,
  WorkflowReminderTemplate.Custom,
  WorkflowReminderTemplate.RatingRequest,
  WorkflowReminderTemplate.ThankYou
];

export function isEmailAction(action: WorkflowAction): boolean {
  return (
    action === WorkflowAction.EmailHost ||
    action === WorkflowAction.EmailAttendee ||
    action === WorkflowAction.EmailAddress
  );
}

export function actionRequiresSendTo(action: WorkflowAction): boolean {
  return (
    action === WorkflowAction.EmailAddress ||
    action === WorkflowAction.SmsNumber ||
    action === WorkflowAction.WhatsappNumber
  );
}

export interface WorkflowStepDraft {
  /** Identifier used for keying. `null` when the step has not yet been persisted. */
  id: string | null;
  action: WorkflowAction;
  template: WorkflowReminderTemplate;
  reminderTime: number | null;
  timeUnit: WorkflowTimeUnit | null;
  sendTo: string | null;
  emailSubject: string | null;
  emailBody: string | null;
}

export function stepToDraft(step: WorkflowStep): WorkflowStepDraft {
  return {
    id: step.id,
    action: step.action,
    template: step.template,
    reminderTime: step.reminderTime,
    timeUnit: step.timeUnit,
    sendTo: step.sendTo,
    emailSubject: step.emailSubject,
    emailBody: step.emailBody
  };
}

export function newStepDraft(trigger: WorkflowTrigger): WorkflowStepDraft {
  return {
    id: null,
    action: WorkflowAction.EmailAttendee,
    template: WorkflowReminderTemplate.Reminder,
    reminderTime: triggerSupportsDelay(trigger) ? 24 : null,
    timeUnit: triggerSupportsDelay(trigger) ? WorkflowTimeUnit.Hours : null,
    sendTo: null,
    emailSubject: null,
    emailBody: null
  };
}

export function isStepDirty(draft: WorkflowStepDraft, step: WorkflowStep | undefined): boolean {
  if (!step) return true;
  return !(
    draft.action === step.action &&
    draft.template === step.template &&
    draft.reminderTime === step.reminderTime &&
    draft.timeUnit === step.timeUnit &&
    (draft.sendTo ?? null) === step.sendTo &&
    (draft.emailSubject ?? null) === step.emailSubject &&
    (draft.emailBody ?? null) === step.emailBody
  );
}

/** Normalizes blank strings to null before sending to the API. */
export function nullIfBlank(value: string | null | undefined): string | null {
  if (value === null || value === undefined) return null;
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

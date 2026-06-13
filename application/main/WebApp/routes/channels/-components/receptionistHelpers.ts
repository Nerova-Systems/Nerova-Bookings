import { t } from "@lingui/core/macro";

import type { Schemas } from "@/shared/lib/api/client";

import {
  awaitingJobRunsQueryKey,
  completedJobRunsQueryKey,
  receptionistEscalationsQueryKey,
  receptionistPoliciesQueryKey,
  receptionistSettingsQueryKey
} from "@/shared/lib/receptionist/queries";

export type ReceptionistSettings = Schemas["ReceptionistSettingsResponse"];

export const settingsQueryKey = receptionistSettingsQueryKey;
export const escalationsQueryKey = receptionistEscalationsQueryKey;
export { awaitingJobRunsQueryKey, completedJobRunsQueryKey };
export const policiesQueryKey = receptionistPoliciesQueryKey;

export function enabledMessage(isEnabled: boolean) {
  return isEnabled
    ? t`Your AI receptionist is answering WhatsApp`
    : t`Your AI receptionist is off — messages use the guided booking flow`;
}

export function settingsBody(
  settings: ReceptionistSettings,
  isEnabled: boolean = settings.isEnabled
): ReceptionistSettings {
  return {
    faqNotes: settings.faqNotes,
    isEnabled,
    languages: settings.languages,
    ownerPhoneNumber: settings.ownerPhoneNumber,
    tone: settings.tone
  };
}

import { t } from "@lingui/core/macro";

import type { Schemas } from "@/shared/lib/api/client";

export type ReceptionistSettings = Schemas["ReceptionistSettingsResponse"];

export const settingsQueryKey = ["get", "/api/main/receptionist/settings"] as const;
export const escalationsQueryKey = ["get", "/api/main/receptionist/escalations"] as const;
export const awaitingJobRunsQueryKey = [
  "get",
  "/api/main/autonomy/job-runs",
  { params: { query: { Status: "AwaitingApproval" } } }
];
export const completedJobRunsQueryKey = ["get", "/api/main/autonomy/job-runs", { params: { query: { Status: "Completed" } } }];
export const policiesQueryKey = ["get", "/api/main/autonomy/policies"] as const;

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

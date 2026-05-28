import { t } from "@lingui/core/macro";

import { AppCategory, type Schemas } from "@/shared/lib/api/client";

export type App = Schemas["AppResponse"];
export type ApiValidationError = Schemas["HttpValidationProblemDetails"] | null | undefined;

/**
 * Order Apps are grouped in the Installed Apps page. Calendar comes first because conferencing
 * apps (Google Meet, MS Teams) reuse a calendar credential and the user must install the
 * matching calendar connector before the conferencing one. Payment / Other are placeholders
 * for future tracks.
 */
export const APP_CATEGORY_ORDER: readonly AppCategory[] = [
  AppCategory.Calendar,
  AppCategory.Conferencing,
  AppCategory.Payment,
  AppCategory.Other
];

export function getAppCategoryLabel(category: AppCategory): string {
  switch (category) {
    case AppCategory.Calendar:
      return t`Calendar`;
    case AppCategory.Conferencing:
      return t`Conferencing`;
    case AppCategory.Payment:
      return t`Payment`;
    case AppCategory.Other:
      return t`Other`;
  }
}

/**
 * Conferencing apps reuse a host calendar credential and therefore require the matching
 * calendar connector to be installed first. Mirrors the prerequisite enforced server-side
 * by GoogleMeetInstaller and MsTeamsInstaller.
 */
const CONFERENCING_PREREQUISITE: Readonly<Record<string, string>> = {
  "google-meet": "google-calendar",
  "ms-teams": "office365-calendar"
};

export function getPrerequisiteSlug(slug: string): string | null {
  return CONFERENCING_PREREQUISITE[slug] ?? null;
}

export function getMissingPrerequisite(app: App, allApps: readonly App[]): App | null {
  const prerequisiteSlug = getPrerequisiteSlug(app.slug);
  if (prerequisiteSlug === null) return null;
  const prerequisite = allApps.find((candidate) => candidate.slug === prerequisiteSlug);
  if (prerequisite === undefined) return null;
  return prerequisite.isConnectedForUser ? null : prerequisite;
}

export function getApiErrorMessages(error: ApiValidationError): string[] {
  return [error?.detail, ...Object.values(error?.errors ?? {}).flat()].filter(
    (value): value is string => typeof value === "string" && value.length > 0
  );
}

export function groupAppsByCategory(apps: readonly App[]): Array<{ category: AppCategory; apps: App[] }> {
  return APP_CATEGORY_ORDER.map((category) => ({
    category,
    apps: apps.filter((app) => app.category === category)
  })).filter((group) => group.apps.length > 0);
}

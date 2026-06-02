import { t } from "@lingui/core/macro";

import { AppCategory, type Schemas } from "@/shared/lib/api/client";

export type App = Schemas["AppResponse"];
export type AppPermission = Schemas["AppPermission"];
export type ApiValidationError = Schemas["HttpValidationProblemDetails"] | null | undefined;

/**
 * Apps that are intentionally hidden from the App Store and Installed Apps pages. WhatsApp Business
 * lives in its own /channels section, so it must never appear in the generic app grid, the category
 * counts, or the installed list.
 */
const HIDDEN_APP_SLUGS: ReadonlySet<string> = new Set(["whatsapp"]);

/** Removes apps that are surfaced elsewhere (e.g. WhatsApp -> /channels) from a list. */
export function getVisibleApps(apps: readonly App[]): App[] {
  return apps.filter((app) => !HIDDEN_APP_SLUGS.has(app.slug));
}

/**
 * Slugs for which a local SVG icon has been copied into the WebApp public assets
 * (`public/app-icons/<slug>.svg`). For these we prefer the bundled asset over the remote logoUrl.
 */
const LOCAL_ICON_SLUGS: ReadonlySet<string> = new Set([
  "google-calendar",
  "office365-calendar",
  "zoom",
  "google-meet",
  "ms-teams"
]);

/** Prefer the locally bundled app icon, falling back to the API-provided logoUrl. */
export function getAppIconSrc(app: App): string {
  return LOCAL_ICON_SLUGS.has(app.slug) ? `/app-icons/${app.slug}.svg` : app.logoUrl;
}

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

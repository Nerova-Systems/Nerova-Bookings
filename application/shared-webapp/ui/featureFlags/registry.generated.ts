// AUTO-GENERATED FROM application/shared-kernel/SharedKernel/FeatureFlags/FeatureFlags.cs.
// Regenerate with `dotnet run --project developer-cli -- build --backend`. Do not edit by hand.
//
// Carries the runtime metadata that `useFeatureFlag` needs to evaluate a flag client-side. System
// flags additionally carry the frontend env-var name so the hook can read from import.meta.runtime_env.
//
// FeatureFlagKey is the union of every key defined in FeatureFlags.cs. Hook + helper signatures
// accept this union instead of `string`, so deleting or renaming a backend flag turns every
// `useFeatureFlag(deletedKey)` and `getFeatureFlagLabel(deletedKey)` callsite into a TS compile
// error after the next backend build regenerates this file.

export type FeatureFlagKey = "google-oauth" | "subscriptions" | "beta-features" | "sso" | "account-overview" | "compact-view" | "experimental-ui" | "tier-teams" | "tier-organizations" | "tier-enterprise" | "cap-managed-event-types" | "cap-round-robin" | "cap-collective" | "cap-attributes" | "cap-custom-smtp" | "cap-org-billing" | "cap-delegation-credentials" | "cap-sso-microsoft" | "cap-sso-google" | "cap-audit-log" | "cap-workflows" | "cap-api-keys" | "cap-impersonation" | "cap-insights";

type FeatureFlagScope = "system" | "tenant" | "user";
type FeatureFlagAdminLevel = "systemAdmin" | "tenantOwner" | "user";

type BaseFeatureFlagDefinition = {
  key: FeatureFlagKey;
  scope: FeatureFlagScope;
  adminLevel: FeatureFlagAdminLevel;
  parentDependency: FeatureFlagKey | null;
  description: string;
};

type SystemFeatureFlagDefinition = BaseFeatureFlagDefinition & {
  scope: "system";
  envVar: string;
};

type DatabaseFeatureFlagDefinition = BaseFeatureFlagDefinition & {
  scope: "tenant" | "user";
};

export type FeatureFlagDefinition = SystemFeatureFlagDefinition | DatabaseFeatureFlagDefinition;

const featureFlagRegistry: Record<FeatureFlagKey, FeatureFlagDefinition> = {
    "google-oauth": {
      key: "google-oauth",
      scope: "system",
      adminLevel: "systemAdmin",
      parentDependency: null,
      description: "Sign in with Google using OpenID Connect",
      envVar: "PUBLIC_GOOGLE_OAUTH_ENABLED"
    },
    "subscriptions": {
      key: "subscriptions",
      scope: "system",
      adminLevel: "systemAdmin",
      parentDependency: null,
      description: "Stripe-powered subscription billing and plan management",
      envVar: "PUBLIC_SUBSCRIPTION_ENABLED"
    },
    "beta-features": {
      key: "beta-features",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: null,
      description: "Early access to experimental features before general availability"
    },
    "sso": {
      key: "sso",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: null,
      description: "Allow users to authenticate using enterprise identity providers"
    },
    "account-overview": {
      key: "account-overview",
      scope: "tenant",
      adminLevel: "tenantOwner",
      parentDependency: null,
      description: "Show the account overview dashboard with user statistics at /account. When disabled, signed-in users go straight to the users list."
    },
    "compact-view": {
      key: "compact-view",
      scope: "user",
      adminLevel: "user",
      parentDependency: null,
      description: "Reduce spacing between UI elements for a denser layout"
    },
    "experimental-ui": {
      key: "experimental-ui",
      scope: "user",
      adminLevel: "user",
      parentDependency: null,
      description: "Try out experimental user interface components"
    },
    "tier-teams": {
      key: "tier-teams",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: null,
      description: "Enables team-level functionality: team management, team-scoped event types and schedules, round-robin and collective scheduling"
    },
    "tier-organizations": {
      key: "tier-organizations",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-teams",
      description: "Enables organization-level functionality: org management, org-scoped attributes, custom SMTP, billing, delegation credentials, and SSO"
    },
    "tier-enterprise": {
      key: "tier-enterprise",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-organizations",
      description: "Enables enterprise-only functionality: audit log, workflows, API keys, impersonation, and analytics insights"
    },
    "cap-managed-event-types": {
      key: "cap-managed-event-types",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-teams",
      description: "Team-owned event types with locked fields that members inherit. Ports cal.com managed-event-types."
    },
    "cap-round-robin": {
      key: "cap-round-robin",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-teams",
      description: "Distribute bookings across available team members in rotation. Ports cal.com round-robin."
    },
    "cap-collective": {
      key: "cap-collective",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-teams",
      description: "Require all listed team members to be available before a slot is offered to bookers. Ports cal.com collective scheduling."
    },
    "cap-attributes": {
      key: "cap-attributes",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-organizations",
      description: "Org-defined custom fields attached to memberships, e.g. department, skills, timezone. Ports cal.com attributes."
    },
    "cap-custom-smtp": {
      key: "cap-custom-smtp",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-organizations",
      description: "Per-org SMTP server override so org-scoped emails are sent from the org's own mail domain. Ports cal.com custom-smtp."
    },
    "cap-org-billing": {
      key: "cap-org-billing",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-organizations",
      description: "Seat-based billing and subscription management at the organization level. Ports cal.com billing/organizations. Requires g3-org-billing."
    },
    "cap-delegation-credentials": {
      key: "cap-delegation-credentials",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-organizations",
      description: "Multi-tenant Google/Microsoft OAuth so the org can read calendar busy-time and create conferencing links on behalf of members. Ports cal.com delegation-credentials."
    },
    "cap-sso-microsoft": {
      key: "cap-sso-microsoft",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-organizations",
      description: "Allow org members to sign in via Microsoft Entra ID / Azure AD. Ports cal.com Microsoft SSO. Requires g3-sso-microsoft."
    },
    "cap-sso-google": {
      key: "cap-sso-google",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-organizations",
      description: "Allow org members to sign in via Google Workspace. Ports cal.com Google SSO. Requires g3-sso-google."
    },
    "cap-audit-log": {
      key: "cap-audit-log",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-enterprise",
      description: "Immutable record of all significant system events across every SCS, written via the shared-kernel event bus. Ports cal.com booking-audit."
    },
    "cap-workflows": {
      key: "cap-workflows",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-enterprise",
      description: "Automated booking reminders, follow-ups, and no-show handling. Ports cal.com workflows. Requires g3-workflows."
    },
    "cap-api-keys": {
      key: "cap-api-keys",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-enterprise",
      description: "Generate long-lived API keys for programmatic access at the user or org level. Ports cal.com api-keys."
    },
    "cap-impersonation": {
      key: "cap-impersonation",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-enterprise",
      description: "Allow system admins to impersonate any user account for support and debugging, with a full audit trail. Ports cal.com impersonation."
    },
    "cap-insights": {
      key: "cap-insights",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "tier-enterprise",
      description: "Analytics dashboard: booking volume, event-type performance, and member load metrics. Ports cal.com insights. Requires g3-insights."
    }
};

export function getFlag(key: FeatureFlagKey): FeatureFlagDefinition {
  return featureFlagRegistry[key];
}

export function getAllFlags(): FeatureFlagDefinition[] {
  return Object.values(featureFlagRegistry);
}

export { featureFlagRegistry };

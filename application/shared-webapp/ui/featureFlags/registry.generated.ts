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

export type FeatureFlagKey = "google-oauth" | "subscriptions" | "beta-features" | "sso" | "account-overview" | "compact-view" | "experimental-ui" | "cal-com-core" | "cal-com-event-types" | "cal-com-availability" | "cal-com-public-booking" | "cal-com-bookings" | "cal-com-workflows" | "cal-com-webhooks" | "cal-com-apps-connectors" | "cal-com-conferencing" | "cal-com-teams-organizations" | "cal-com-embeds" | "cal-com-payments" | "cal-com-api-compatibility";

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
    "cal-com-core": {
      key: "cal-com-core",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: null,
      description: "Expose the imported Cal.com product layer after parity validation"
    },
    "cal-com-event-types": {
      key: "cal-com-event-types",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com event type setup and management after parity validation"
    },
    "cal-com-availability": {
      key: "cal-com-availability",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com schedules, availability, slots, and busy-time behavior after parity validation"
    },
    "cal-com-public-booking": {
      key: "cal-com-public-booking",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com public web booking after parity validation"
    },
    "cal-com-bookings": {
      key: "cal-com-bookings",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com booking lifecycle and booking management after parity validation"
    },
    "cal-com-workflows": {
      key: "cal-com-workflows",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com workflow automation after parity validation"
    },
    "cal-com-webhooks": {
      key: "cal-com-webhooks",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com webhook behavior after parity validation"
    },
    "cal-com-apps-connectors": {
      key: "cal-com-apps-connectors",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com app-store and connector behavior after parity validation"
    },
    "cal-com-conferencing": {
      key: "cal-com-conferencing",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com conferencing integrations after parity validation"
    },
    "cal-com-teams-organizations": {
      key: "cal-com-teams-organizations",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com team and organization behavior after parity validation"
    },
    "cal-com-embeds": {
      key: "cal-com-embeds",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com embed behavior after parity validation"
    },
    "cal-com-payments": {
      key: "cal-com-payments",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com payment behavior after parity validation"
    },
    "cal-com-api-compatibility": {
      key: "cal-com-api-compatibility",
      scope: "tenant",
      adminLevel: "systemAdmin",
      parentDependency: "cal-com-core",
      description: "Expose Cal.com API compatibility routes after parity validation"
    }
};

export function getFlag(key: FeatureFlagKey): FeatureFlagDefinition {
  return featureFlagRegistry[key];
}

export function getAllFlags(): FeatureFlagDefinition[] {
  return Object.values(featureFlagRegistry);
}

export { featureFlagRegistry };

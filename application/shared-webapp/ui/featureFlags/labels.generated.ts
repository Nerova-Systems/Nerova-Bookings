// AUTO-GENERATED FROM application/shared-kernel/SharedKernel/FeatureFlags/FeatureFlags.cs.
// Regenerate with `dotnet run --project developer-cli -- build --backend`. Do not edit by hand.
//
// The English copy here mirrors the Label and Description fields on each FeatureFlagDefinition.
// Lingui extracts these strings at frontend build time into shared-webapp/ui/translations/locale/*.po
// for translators to localize.
//
// Helpers accept `string` (not the strict FeatureFlagKey union) because flag keys arrive here
// from API responses too — values that the type system can't pin to the current set. Unknown keys
// fall back to a humanized form so historical telemetry and stale tenant overrides still display
// readably. Strict-typing for hard-coded keys happens at the `useFeatureFlag` hook, not here.

import { t } from "@lingui/core/macro";

interface FeatureFlagLabel {
  name: string;
  description: string;
}

function getKnownFeatureFlagLabels(): Record<string, FeatureFlagLabel> {
  return {
    "google-oauth": {
      name: t`Google OAuth`,
      description: t`Sign in with Google using OpenID Connect`
    },
    "subscriptions": {
      name: t`Subscriptions`,
      description: t`Stripe-powered subscription billing and plan management`
    },
    "beta-features": {
      name: t`Beta features`,
      description: t`Early access to experimental features before general availability`
    },
    "sso": {
      name: t`Single sign-on`,
      description: t`Allow users to authenticate using enterprise identity providers`
    },
    "account-overview": {
      name: t`Account overview page`,
      description: t`Show the account overview dashboard with user statistics at /account. When disabled, signed-in users go straight to the users list.`
    },
    "compact-view": {
      name: t`Compact view`,
      description: t`Reduce spacing between UI elements for a denser layout`
    },
    "experimental-ui": {
      name: t`Experimental UI`,
      description: t`Try out experimental user interface components`
    },
    "tier-teams": {
      name: t`Teams tier`,
      description: t`Enables team-level functionality: team management, team-scoped event types and schedules, round-robin and collective scheduling`
    },
    "tier-organizations": {
      name: t`Organizations tier`,
      description: t`Enables organization-level functionality: org management, org-scoped attributes, custom SMTP, billing, delegation credentials, and SSO`
    },
    "tier-enterprise": {
      name: t`Enterprise tier`,
      description: t`Enables enterprise-only functionality: audit log, workflows, API keys, impersonation, and analytics insights`
    },
    "cap-managed-event-types": {
      name: t`Managed event types`,
      description: t`Team-owned event types with locked fields that members inherit. Ports cal.com managed-event-types.`
    },
    "cap-round-robin": {
      name: t`Round-robin scheduling`,
      description: t`Distribute bookings across available team members in rotation. Ports cal.com round-robin.`
    },
    "cap-collective": {
      name: t`Collective scheduling`,
      description: t`Require all listed team members to be available before a slot is offered to bookers. Ports cal.com collective scheduling.`
    },
    "cap-attributes": {
      name: t`Member attributes`,
      description: t`Org-defined custom fields attached to memberships, e.g. department, skills, timezone. Ports cal.com attributes.`
    },
    "cap-custom-smtp": {
      name: t`Custom SMTP`,
      description: t`Per-org SMTP server override so org-scoped emails are sent from the org's own mail domain. Ports cal.com custom-smtp.`
    },
    "cap-org-billing": {
      name: t`Org billing`,
      description: t`Seat-based billing and subscription management at the organization level. Ports cal.com billing/organizations. Requires g3-org-billing.`
    },
    "cap-delegation-credentials": {
      name: t`Delegation credentials`,
      description: t`Multi-tenant Google/Microsoft OAuth so the org can read calendar busy-time and create conferencing links on behalf of members. Ports cal.com delegation-credentials.`
    },
    "cap-sso-microsoft": {
      name: t`Microsoft SSO`,
      description: t`Allow org members to sign in via Microsoft Entra ID / Azure AD. Ports cal.com Microsoft SSO. Requires g3-sso-microsoft.`
    },
    "cap-sso-google": {
      name: t`Google SSO`,
      description: t`Allow org members to sign in via Google Workspace. Ports cal.com Google SSO. Requires g3-sso-google.`
    },
    "cap-integration-attribute-sync": {
      name: t`IdP attribute sync`,
      description: t`Automatically sync user attributes from SAML/SCIM/SSO claims into org member profiles on every SSO login. Ports cal.com IdP attribute sync.`
    },
    "cap-audit-log": {
      name: t`Audit log`,
      description: t`Immutable record of all significant system events across every SCS, written via the shared-kernel event bus. Ports cal.com booking-audit.`
    },
    "cap-workflows": {
      name: t`Workflows`,
      description: t`Automated booking reminders, follow-ups, and no-show handling. Ports cal.com workflows. Requires g3-workflows.`
    },
    "cap-api-keys": {
      name: t`API keys`,
      description: t`Generate long-lived API keys for programmatic access at the user or org level. Ports cal.com api-keys.`
    },
    "cap-impersonation": {
      name: t`Impersonation`,
      description: t`Allow system admins to impersonate any user account for support and debugging, with a full audit trail. Ports cal.com impersonation.`
    },
    "cap-insights": {
      name: t`Insights`,
      description: t`Analytics dashboard: booking volume, event-type performance, and member load metrics. Ports cal.com insights. Requires g3-insights.`
    }
  };
}

function formatFeatureFlagKey(flagKey: string): string {
  const formatted = flagKey.replace(/-/g, " ");
  return formatted.charAt(0).toUpperCase() + formatted.slice(1);
}

export function getFeatureFlagLabel(flagKey: string): FeatureFlagLabel {
  const known = getKnownFeatureFlagLabels()[flagKey];
  if (known) return known;
  const name = formatFeatureFlagKey(flagKey);
  return { name, description: name };
}

export function getFeatureFlagName(flagKey: string): string {
  return getFeatureFlagLabel(flagKey).name;
}

export function getFeatureFlagDescription(flagKey: string): string {
  return getFeatureFlagLabel(flagKey).description;
}

export function getFeatureFlagSourceLabel(source: string): string {
  switch (source) {
    case "manual_override":
      return t`Manual override`;
    case "ab_rollout":
      return t`A/B rollout`;
    case "plan":
      return t`Plan`;
    case "default":
      return t`Default`;
    default:
      return source;
  }
}

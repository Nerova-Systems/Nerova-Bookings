namespace SharedKernel.FeatureFlags;

// Add a new feature flag by declaring a `public static readonly FeatureFlagDefinition` field below.
// The registry in FeatureFlagsRegistry.cs picks it up by reflection at startup — no manual list
// maintenance, no DI wiring, no JSON. Choose the subtype (SystemFeatureFlag, TenantAbTestFlag,
// PlanGatedTenantFlag, TenantOwnerConfigurableFlag, UserAbTestFlag, UserConfigurableFlag) that
// matches how the flag should be evaluated and who is allowed to change it.
//
// On startup, the Account Worker's FeatureFlagDefinitionReconciler upserts a row in the
// feature_flags table for every non-System flag declared here, so it shows up in the Back Office
// immediately after deployment — no migration, no seed script. SystemFeatureFlag definitions are
// evaluated from config and env vars instead, so they never get a DB row. Removing a flag marks
// its row as orphaned but keeps it visible until you hard-delete it from the Back Office.
//
// The label and description below are also surfaced in the frontend: `build --backend` runs the
// GenerateFeatureFlagsManifest MSBuild target which emits featureFlags.generated.json, and the
// shared-webapp generateFeatureFlagArtifacts.mjs script turns that into labels.generated.ts with
// Lingui `t` macros. Lingui extraction then writes the strings into the locale .po files for
// translators, so every flag added here automatically becomes translatable in the UI.
[PublicAPI]
public static partial class FeatureFlags
{
    public static readonly FeatureFlagDefinition GoogleOauth = new SystemFeatureFlag(
        "google-oauth",
        "Google OAuth",
        "Sign in with Google using OpenID Connect",
        "OAuth:Google:ClientId",
        "PUBLIC_GOOGLE_OAUTH_ENABLED",
        false
    );

    public static readonly FeatureFlagDefinition Subscriptions = new SystemFeatureFlag(
        "subscriptions",
        "Subscriptions",
        "Stripe-powered subscription billing and plan management",
        "Stripe:SubscriptionEnabled",
        "PUBLIC_SUBSCRIPTION_ENABLED",
        false,
        "true"
    );

    public static readonly FeatureFlagDefinition BetaFeatures = new TenantAbTestFlag(
        "beta-features",
        "Beta features",
        "Early access to experimental features before general availability",
        true,
        true
    );

    public static readonly FeatureFlagDefinition Sso = new PlanGatedTenantFlag(
        "sso",
        "Single sign-on",
        "Allow users to authenticate using enterprise identity providers",
        PlanTier.Premium,
        false
    );

    public static readonly FeatureFlagDefinition AccountOverview = new TenantOwnerConfigurableFlag(
        "account-overview",
        "Account overview page",
        "Show the account overview dashboard with user statistics at /account. When disabled, signed-in users go straight to the users list.",
        true,
        true
    );

    public static readonly FeatureFlagDefinition CompactView = new UserConfigurableFlag(
        "compact-view",
        "Compact view",
        "Reduce spacing between UI elements for a denser layout",
        true,
        true
    );

    public static readonly FeatureFlagDefinition ExperimentalUi = new UserAbTestFlag(
        "experimental-ui",
        "Experimental UI",
        "Try out experimental user interface components",
        true,
        true
    );

    // -----------------------------------------------------------------------------------------
    // Tier flags
    //
    // Three mutually-inclusive-upward tiers driven by explicit per-tenant admin grants:
    //   tier-teams ← tier-organizations ← tier-enterprise
    //
    // "Mutually-inclusive upward" means that enabling a higher tier requires the lower tiers
    // to be enabled first. The ParentDependency chain enforces this in the evaluator: granting
    // tier-organizations without also granting tier-teams leaves the evaluator's parent check
    // unsatisfied, keeping tier-organizations inactive. Admins must grant all tiers in the chain.
    //
    // All tier flags ship OFF by default (IsKillSwitchEnabled=true → reconciler creates the base
    // row inactive). Plan-tier auto-grants (e.g., "Enterprise plan → tier-enterprise") are deferred
    // until g3-org-billing lands Paystack org billing.
    //
    // TODO(g3-org-billing): add PlanTier dependency so Org/Enterprise plan tenants auto-receive
    // the appropriate tier flag without a manual admin grant.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    ///     Gates all team-level functionality in the cal.com port: managed event types, round-robin
    ///     and collective scheduling, team-scoped schedules, and team membership management.
    ///     This is the base tier; tier-organizations and tier-enterprise both build on top of it.
    /// </summary>
    public static readonly FeatureFlagDefinition TierTeams = new TenantAdminManagedFlag(
        "tier-teams",
        "Teams tier",
        "Enables team-level functionality: team management, team-scoped event types and schedules, round-robin and collective scheduling",
        false,
        true
    );

    /// <summary>
    ///     Gates all organization-level functionality in the cal.com port: org-wide attributes,
    ///     custom SMTP per org, org billing, delegation credentials, and org-scoped SSO.
    ///     Requires tier-teams (parent dependency) — a tenant must have Teams tier active before
    ///     Organizations tier can evaluate true.
    /// </summary>
    public static readonly FeatureFlagDefinition TierOrganizations = new TenantAdminManagedFlag(
        "tier-organizations",
        "Organizations tier",
        "Enables organization-level functionality: org management, org-scoped attributes, custom SMTP, billing, delegation credentials, and SSO",
        false,
        true,
        parentDependency: "tier-teams"
    );

    /// <summary>
    ///     Gates enterprise-only functionality in the cal.com port: audit log, automations/workflows,
    ///     API keys, user impersonation, and insights analytics.
    ///     Requires tier-organizations (parent dependency) — a tenant must have Organizations tier
    ///     active before Enterprise tier can evaluate true.
    /// </summary>
    public static readonly FeatureFlagDefinition TierEnterprise = new TenantAdminManagedFlag(
        "tier-enterprise",
        "Enterprise tier",
        "Enables enterprise-only functionality: audit log, workflows, API keys, impersonation, and analytics insights",
        false,
        true,
        parentDependency: "tier-organizations"
    );

    // -----------------------------------------------------------------------------------------
    // Capability flags — tier-teams
    //
    // Feature-level gates for capabilities delivered in Wave 2 leaf slices that target the
    // Teams tier. Each flag is inactive by default and requires both the capability flag itself
    // and its tier-teams parent to be granted for a tenant.
    //
    // TODO(g3-org-billing): capability auto-grants follow tier auto-grants.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    ///     Gates managed event types (parent event types locked to team members).
    ///     Ports cal.com packages/features/ee/managed-event-types.
    ///     Gated on tier-teams via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapManagedEventTypes = new TenantAdminManagedFlag(
        "cap-managed-event-types",
        "Managed event types",
        "Team-owned event types with locked fields that members inherit. Ports cal.com managed-event-types.",
        false,
        true,
        parentDependency: "tier-teams"
    );

    /// <summary>
    ///     Gates round-robin scheduling (distribute bookings across team members by availability).
    ///     Ports cal.com packages/features/ee/round-robin.
    ///     Gated on tier-teams via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapRoundRobin = new TenantAdminManagedFlag(
        "cap-round-robin",
        "Round-robin scheduling",
        "Distribute bookings across available team members in rotation. Ports cal.com round-robin.",
        false,
        true,
        parentDependency: "tier-teams"
    );

    /// <summary>
    ///     Gates collective scheduling (all listed hosts must be free for a slot to appear).
    ///     Ports cal.com collective scheduling algorithm.
    ///     Gated on tier-teams via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapCollective = new TenantAdminManagedFlag(
        "cap-collective",
        "Collective scheduling",
        "Require all listed team members to be available before a slot is offered to bookers. Ports cal.com collective scheduling.",
        false,
        true,
        parentDependency: "tier-teams"
    );

    // -----------------------------------------------------------------------------------------
    // Capability flags — tier-organizations
    //
    // Feature-level gates for capabilities that require the Organizations tier. Each flag is
    // inactive by default; both this flag and tier-organizations (and transitively tier-teams)
    // must be active for the capability to evaluate true.
    //
    // TODO(g3-org-billing): capability auto-grants follow tier auto-grants.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    ///     Gates org-defined custom attributes on memberships (e.g., department, skills).
    ///     Ports cal.com packages/features/attributes.
    ///     Gated on tier-organizations via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapAttributes = new TenantAdminManagedFlag(
        "cap-attributes",
        "Member attributes",
        "Org-defined custom fields attached to memberships, e.g. department, skills, timezone. Ports cal.com attributes.",
        false,
        true,
        parentDependency: "tier-organizations"
    );

    /// <summary>
    ///     Gates per-org custom SMTP configuration (override default mailer for org-scoped email).
    ///     Ports cal.com packages/features/ee/organizations custom-smtp.
    ///     Gated on tier-organizations via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapCustomSmtp = new TenantAdminManagedFlag(
        "cap-custom-smtp",
        "Custom SMTP",
        "Per-org SMTP server override so org-scoped emails are sent from the org's own mail domain. Ports cal.com custom-smtp.",
        false,
        true,
        parentDependency: "tier-organizations"
    );

    /// <summary>
    ///     Gates organization-level billing management (seat-based plans, invoices, usage).
    ///     Ports cal.com packages/features/ee/billing/organizations.
    ///     Blocked on g3-org-billing (Paystack Split design) — activating this flag before
    ///     g3-org-billing lands has no effect because the backend module is not yet implemented.
    ///     Gated on tier-organizations via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapOrgBilling = new TenantAdminManagedFlag(
        "cap-org-billing",
        "Org billing",
        "Seat-based billing and subscription management at the organization level. Ports cal.com billing/organizations. Requires g3-org-billing.",
        false,
        true,
        parentDependency: "tier-organizations"
    );

    /// <summary>
    ///     Gates delegation credentials (multi-tenant Google / Microsoft OAuth for calendar
    ///     busy-time and conferencing on behalf of org members).
    ///     Ports cal.com packages/features/ee/delegation-credentials.
    ///     Gated on tier-organizations via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapDelegationCredentials = new TenantAdminManagedFlag(
        "cap-delegation-credentials",
        "Delegation credentials",
        "Multi-tenant Google/Microsoft OAuth so the org can read calendar busy-time and create conferencing links on behalf of members. Ports cal.com delegation-credentials.",
        false,
        true,
        parentDependency: "tier-organizations"
    );

    /// <summary>
    ///     Gates Microsoft Entra ID / Azure AD SSO login for org members.
    ///     Ports cal.com packages/features/ee/sso (Microsoft path). Engine = Microsoft.Identity.Web.
    ///     Blocked on g3-sso-microsoft until per-org tenant/issuer config is implemented.
    ///     Gated on tier-organizations via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapSsoMicrosoft = new TenantAdminManagedFlag(
        "cap-sso-microsoft",
        "Microsoft SSO",
        "Allow org members to sign in via Microsoft Entra ID / Azure AD. Ports cal.com Microsoft SSO. Requires g3-sso-microsoft.",
        false,
        true,
        parentDependency: "tier-organizations"
    );

    /// <summary>
    ///     Gates Google Workspace SSO login for org members.
    ///     Ports cal.com packages/features/ee/sso (Google path). Engine = Google.Apis.Auth / OIDC.
    ///     Blocked on g3-sso-google until per-org client config is implemented.
    ///     Gated on tier-organizations via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapSsoGoogle = new TenantAdminManagedFlag(
        "cap-sso-google",
        "Google SSO",
        "Allow org members to sign in via Google Workspace. Ports cal.com Google SSO. Requires g3-sso-google.",
        false,
        true,
        parentDependency: "tier-organizations"
    );

    // -----------------------------------------------------------------------------------------
    // Capability flags — tier-enterprise
    //
    // Feature-level gates for enterprise-only capabilities. Each flag is inactive by default;
    // the full chain (capability + tier-enterprise + tier-organizations + tier-teams) must be
    // active for a tenant to use the feature.
    //
    // TODO(g3-org-billing): capability auto-grants follow tier auto-grants.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    ///     Gates the immutable audit log — all significant system events written by every SCS
    ///     via the shared-kernel event bus land in a single audit table.
    ///     Ports cal.com packages/features/booking-audit.
    ///     Gated on tier-enterprise via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapAuditLog = new TenantAdminManagedFlag(
        "cap-audit-log",
        "Audit log",
        "Immutable record of all significant system events across every SCS, written via the shared-kernel event bus. Ports cal.com booking-audit.",
        false,
        true,
        parentDependency: "tier-enterprise"
    );

    /// <summary>
    ///     Gates booking automations: pre-booking reminders, post-booking follow-ups, no-show flows.
    ///     Ports cal.com packages/features/ee/workflows.
    ///     Blocked on g3-workflows (job runner decision) until WorkflowRunner infra is in place.
    ///     Gated on tier-enterprise via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapWorkflows = new TenantAdminManagedFlag(
        "cap-workflows",
        "Workflows",
        "Automated booking reminders, follow-ups, and no-show handling. Ports cal.com workflows. Requires g3-workflows.",
        false,
        true,
        parentDependency: "tier-enterprise"
    );

    /// <summary>
    ///     Gates per-user and per-org API keys for programmatic access.
    ///     Ports cal.com packages/features/ee/api-keys.
    ///     Gated on tier-enterprise via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapApiKeys = new TenantAdminManagedFlag(
        "cap-api-keys",
        "API keys",
        "Generate long-lived API keys for programmatic access at the user or org level. Ports cal.com api-keys.",
        false,
        true,
        parentDependency: "tier-enterprise"
    );

    /// <summary>
    ///     Gates system-admin impersonation of any user account with full audit trail.
    ///     Ports cal.com packages/features/ee/impersonation.
    ///     Gated on tier-enterprise via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapImpersonation = new TenantAdminManagedFlag(
        "cap-impersonation",
        "Impersonation",
        "Allow system admins to impersonate any user account for support and debugging, with a full audit trail. Ports cal.com impersonation.",
        false,
        true,
        parentDependency: "tier-enterprise"
    );

    /// <summary>
    ///     Gates the insights analytics dashboard (booking volume, event-type performance, member load).
    ///     Ports cal.com packages/features/insights.
    ///     Blocked on g3-insights (direct-query vs materialised-view decision).
    ///     Gated on tier-enterprise via ParentDependency.
    /// </summary>
    public static readonly FeatureFlagDefinition CapInsights = new TenantAdminManagedFlag(
        "cap-insights",
        "Insights",
        "Analytics dashboard: booking volume, event-type performance, and member load metrics. Ports cal.com insights. Requires g3-insights.",
        false,
        true,
        parentDependency: "tier-enterprise"
    );
}

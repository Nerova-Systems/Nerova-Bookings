// Public, build-agnostic configuration for the WhatsApp Business Embedded Signup flow.
//
// These values originate from the backend's AddSinglePageAppFallback static runtime environment and
// are surfaced to the SPA through import.meta.runtime_env (see SharedKernel SinglePageApp). They are
// read directly here -- rather than through useFeatureFlag -- because WhatsApp signup is a plain
// PUBLIC_* system toggle (like PUBLIC_SUBSCRIPTION_ENABLED) and is not declared as a typed flag key
// in FeatureFlags.cs, so the codegen FeatureFlagKey union does not include it.
export const whatsAppSignupConfig = {
  isEnabled: import.meta.runtime_env.PUBLIC_WHATSAPP_SIGNUP_ENABLED === "true",
  metaAppId: import.meta.runtime_env.PUBLIC_META_APP_ID,
  metaConfigId: import.meta.runtime_env.PUBLIC_META_CONFIG_ID,
  appUrl: import.meta.runtime_env.PUBLIC_APP_URL
};

// Convenience boolean for gating routes and navigation entries.
export const isWhatsAppSignupEnabled = whatsAppSignupConfig.isEnabled;

/**
 * Meta JS SDK utilities for WhatsApp Business Embedded Signup.
 *
 * Loads the Facebook JS SDK once globally and exposes a promise-based
 * wrapper around FB.login for the Embedded Signup flow.
 *
 * Guard: all exports check `typeof window !== "undefined"` so this module
 * is safe to import in SSR and test environments.
 */

// ─── Types ────────────────────────────────────────────────────────────────────

export type FBAuthResponse = {
  code: string;
  accessToken?: string;
  userID?: string;
  expiresIn?: number;
  data_access_expiration_time?: number;
};

export type FBLoginResponse = {
  status: "connected" | "not_authorized" | "unknown";
  authResponse?: FBAuthResponse;
};

/**
 * Data provided by Meta through the `WA_EMBEDDED_SIGNUP` window message event.
 * Fired during the embedded signup flow after the user selects a phone number.
 */
export type FBEmbeddedSignupData = {
  /** WhatsApp Business Account ID */
  waba_id: string;
  /** Phone number ID linked to the WABA */
  phone_number_id: string;
  /**
   * Human-readable display phone number (e.g. "+27 80 000 0000").
   * Present in newer SDK versions; may be absent in older flows.
   */
  display_phone_number?: string;
};

/**
 * Options for pre-filling the Meta Embedded Signup form
 * with the tenant's known business details.
 */
export type EmbeddedSignupOptions = {
  /** Meta App ID — read from `import.meta.runtime_env.PUBLIC_META_APP_ID` */
  appId: string;
  /** Pre-fill the business name */
  businessName?: string;
  /** Pre-fill the business email */
  businessEmail?: string;
  /** Pre-fill the business phone */
  businessPhone?: string;
  /** Pre-fill the business website */
  businessWebsite?: string;
};

// ─── Window FB type augmentation ─────────────────────────────────────────────

declare global {
  interface Window {
    FB: {
      init(params: { appId: string; version: string; xfbml?: boolean; cookie?: boolean }): void;
      login(callback: (response: FBLoginResponse) => void, options?: Record<string, unknown>): void;
    };
  }
}

// ─── SDK loader ───────────────────────────────────────────────────────────────

/** Meta Graph API version used for SDK initialisation and API calls. */
const FB_API_VERSION = "v21.0";

/** Tracks the in-flight SDK load Promise to avoid double-injecting the script. */
let sdkLoading: Promise<void> | null = null;

/**
 * Injects `https://connect.facebook.net/en_US/sdk.js` once and calls
 * `window.FB.init(...)`. Subsequent calls return the same Promise.
 *
 * Safe to call in environments without a DOM (returns a resolved Promise).
 */
export function loadMetaSDK(appId: string): Promise<void> {
  if (typeof window === "undefined") return Promise.resolve();
  if (window.FB) return Promise.resolve();
  if (sdkLoading) return sdkLoading;

  sdkLoading = new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = "https://connect.facebook.net/en_US/sdk.js";
    script.async = true;
    script.crossOrigin = "anonymous";

    script.onload = () => {
      window.FB.init({
        appId,
        version: FB_API_VERSION,
        xfbml: false,
        cookie: false
      });
      resolve();
    };

    script.onerror = () => {
      sdkLoading = null;
      reject(new Error("Failed to load Meta SDK."));
    };

    document.head.appendChild(script);
  });

  return sdkLoading;
}

// ─── Embedded Signup launcher ─────────────────────────────────────────────────

/**
 * Wraps `window.FB.login` in a Promise for the Embedded Signup flow.
 *
 * Resolves with the `FBAuthResponse` (containing `code`) when the user
 * authorises, or `null` when the popup is closed/cancelled.
 *
 * The SDK must be initialised first via `loadMetaSDK`.
 */
export function launchEmbeddedSignup(options: EmbeddedSignupOptions): Promise<FBAuthResponse | null> {
  if (typeof window === "undefined") return Promise.resolve(null);

  return new Promise((resolve) => {
    window.FB.login(
      (response) => {
        if (response.status === "connected" && response.authResponse) {
          resolve(response.authResponse);
        } else {
          resolve(null);
        }
      },
      {
        scope: "business_management,whatsapp_business_management",
        response_type: "code",
        override_default_response_type: true,
        extras: {
          feature: "whatsapp_embedded_signup",
          setup: options.businessName
            ? {
                business: {
                  name: options.businessName,
                  ...(options.businessEmail ? { email: options.businessEmail } : {}),
                  ...(options.businessPhone ? { phone: options.businessPhone } : {}),
                  ...(options.businessWebsite ? { website: options.businessWebsite } : {})
                }
              }
            : {}
        }
      }
    );
  });
}

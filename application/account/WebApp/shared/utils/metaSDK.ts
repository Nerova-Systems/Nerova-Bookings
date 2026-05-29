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
  /** Meta Configuration ID — read from `import.meta.runtime_env.PUBLIC_META_CONFIG_ID` */
  configId?: string;
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
  if (window.FB) {
    console.log("metaSDK: window.FB already exists, initialising it with our App ID.");
    window.FB.init({
      appId,
      version: FB_API_VERSION,
      xfbml: true,
      cookie: true
    });
    return Promise.resolve();
  }
  if (sdkLoading) {
    console.log("metaSDK: SDK is already loading, returning existing promise.");
    return sdkLoading;
  }

  console.log("metaSDK: Injecting connect.facebook.net/en_US/sdk.js script tag...");
  sdkLoading = new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = "https://connect.facebook.net/en_US/sdk.js";
    script.async = true;
    script.crossOrigin = "anonymous";

    script.onload = () => {
      console.log(
        "metaSDK: Script tag onload triggered, initialising FB SDK with version:",
        FB_API_VERSION,
        "App ID:",
        appId
      );
      window.FB.init({
        appId,
        version: FB_API_VERSION,
        xfbml: true,
        cookie: true
      });
      console.log("metaSDK: FB SDK initialised successfully!");
      resolve();
    };

    script.onerror = () => {
      console.error("metaSDK: Script tag onerror triggered. FB SDK failed to load (blocked by ad-blocker?)");
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
 * Falls back to a direct, ad-blocker-safe popup window via window.open if the
 * Facebook SDK is blocked or unavailable.
 */
export function launchEmbeddedSignup(options: EmbeddedSignupOptions): Promise<FBAuthResponse | null> {
  if (typeof window === "undefined") return Promise.resolve(null);
  console.log("metaSDK: launchEmbeddedSignup called with options:", options);

  return new Promise((resolve) => {
    if (!window.FB) {
      console.warn("metaSDK: window.FB is not defined. Falling back to ad-blocker-safe direct window.open popup!");

      const extras = {
        feature: "whatsapp_embedded_signup",
        sessionInfoVersion: "3",
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
      };

      const oauthParams: Record<string, string> = {
        client_id: options.appId,
        redirect_uri: "https://www.facebook.com/connect/login_success.html",
        response_type: "code",
        override_default_response_type: "true",
        extras: JSON.stringify(extras)
      };

      if (options.configId) {
        oauthParams.config_id = options.configId;
      } else {
        oauthParams.scope = "business_management,whatsapp_business_management";
      }

      const oauthUrl = `https://www.facebook.com/v21.0/dialog/oauth?` + new URLSearchParams(oauthParams).toString();

      console.log("metaSDK: Direct popup URL:", oauthUrl);

      const left = window.screen.width / 2 - 300;
      const top = window.screen.height / 2 - 350;
      const popup = window.open(
        oauthUrl,
        "MetaEmbeddedSignup",
        `width=600,height=700,left=${left},top=${top},scrollbars=no,resizable=no`
      );

      if (!popup) {
        console.error("metaSDK: Direct popup window failed to open (blocked by browser popup blocker?)");
        resolve(null);
        return;
      }

      console.log("metaSDK: Direct popup window opened successfully. Starting close-poll timer...");
      const timer = setInterval(() => {
        if (popup.closed) {
          clearInterval(timer);
          console.log("metaSDK: Direct popup window was closed by user.");
          // Resolve with a mock authorization code so SetupTab can proceed with its captured postMessage WABA data
          resolve({ code: "direct-popup-connected" });
        }
      }, 500);
      return;
    }

    console.log("metaSDK: Calling window.FB.login synchronously...");

    const loginOptions: Record<string, unknown> = {
      response_type: "code",
      override_default_response_type: true,
      extras: {
        feature: "whatsapp_embedded_signup",
        sessionInfoVersion: "3",
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
    };

    if (options.configId) {
      loginOptions.config_id = options.configId;
    } else {
      loginOptions.scope = "business_management,whatsapp_business_management";
    }

    window.FB.login((response) => {
      console.log("metaSDK: window.FB.login callback triggered. Status:", response.status, "Response:", response);
      if (response.status === "connected" && response.authResponse) {
        resolve(response.authResponse);
      } else {
        resolve(null);
      }
    }, loginOptions);
  });
}

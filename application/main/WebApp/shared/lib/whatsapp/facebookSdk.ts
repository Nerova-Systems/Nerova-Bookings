// Minimal loader and ambient typings for the Facebook JS SDK, used by the WhatsApp Business
// Embedded Signup flow. We intentionally avoid pulling in a heavy `@types/facebook-js-sdk`
// dependency and instead declare only the small surface we call (FB.init and FB.login).
//
// The whole flow only works on the public production origin (https://app.nerovasystems.com) because
// Meta rejects localhost. On other origins the SDK still loads, but FB.login will not complete.

interface FacebookInitParameters {
  appId: string;
  version: string;
  xfbml?: boolean;
  cookie?: boolean;
}

export interface FacebookAuthResponse {
  /** Authorization code returned when response_type is "code" (Embedded Signup). */
  code?: string;
  accessToken?: string;
  userID?: string;
  expiresIn?: number;
  signedRequest?: string;
}

export interface FacebookLoginResponse {
  authResponse: FacebookAuthResponse | null;
  status: string;
}

export interface FacebookLoginOptions {
  config_id?: string;
  response_type?: string;
  override_default_response_type?: boolean;
  scope?: string;
  extras?: Record<string, unknown>;
}

interface FacebookSdk {
  init: (parameters: FacebookInitParameters) => void;
  login: (callback: (response: FacebookLoginResponse) => void, options?: FacebookLoginOptions) => void;
}

declare global {
  interface Window {
    FB?: FacebookSdk;
    fbAsyncInit?: () => void;
  }
}

const FACEBOOK_SDK_SOURCE = "https://connect.facebook.net/en_US/sdk.js";
const FACEBOOK_SDK_SCRIPT_ID = "facebook-jssdk";
const FACEBOOK_GRAPH_VERSION = "v21.0";

// Memoize so the SDK script is injected and FB.init runs exactly once per page load even when
// multiple components mount the loader concurrently.
let loadPromise: Promise<void> | null = null;

/**
 * Injects the Facebook JS SDK script (once) and resolves after FB.init has run with the given app
 * id. Subsequent calls return the same promise.
 */
export function loadFacebookSdk(appId: string): Promise<void> {
  if (loadPromise) {
    return loadPromise;
  }

  loadPromise = new Promise<void>((resolve, reject) => {
    const initialize = () => {
      window.FB?.init({ appId, version: FACEBOOK_GRAPH_VERSION, xfbml: false, cookie: true });
      resolve();
    };

    // SDK already present (e.g. hot reload) -- just re-initialize.
    if (window.FB) {
      initialize();
      return;
    }

    window.fbAsyncInit = initialize;

    if (document.getElementById(FACEBOOK_SDK_SCRIPT_ID)) {
      // Script tag exists but fbAsyncInit hasn't fired yet; the handler above will resolve us.
      return;
    }

    const script = document.createElement("script");
    script.id = FACEBOOK_SDK_SCRIPT_ID;
    script.src = FACEBOOK_SDK_SOURCE;
    script.async = true;
    script.defer = true;
    script.crossOrigin = "anonymous";
    script.addEventListener("error", () => {
      // Allow a later retry by clearing the memoized promise.
      loadPromise = null;
      reject(new Error("Failed to load the Facebook SDK."));
    });
    document.body.appendChild(script);
  });

  return loadPromise;
}

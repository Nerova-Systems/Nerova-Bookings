import { useCallback, useEffect, useRef, useState } from "react";

import { loadFacebookSdk } from "./facebookSdk";

export interface EmbeddedSignupPayload {
  code: string;
  wabaId: string;
  phoneNumberId: string;
}

interface UseWhatsAppEmbeddedSignupOptions {
  appId: string;
  configId: string;
  onComplete: (payload: EmbeddedSignupPayload) => void;
}

interface SessionInfo {
  wabaId: string | null;
  phoneNumberId: string | null;
}

// Meta posts Embedded Signup progress events to the opener window. We only trust messages whose
// origin resolves to facebook.com (or a subdomain) to avoid acting on spoofed postMessage traffic.
function isFacebookOrigin(origin: string): boolean {
  try {
    const { hostname } = new URL(origin);
    return hostname === "facebook.com" || hostname.endsWith(".facebook.com");
  } catch {
    return false;
  }
}

/**
 * Drives the Facebook Embedded Signup flow for WhatsApp Business onboarding:
 * 1. Loads the Facebook JS SDK and reports readiness.
 * 2. Listens for `WA_EMBEDDED_SIGNUP` postMessage events to capture the `waba_id` and
 *    `phone_number_id` selected by the user inside Meta's popup.
 * 3. Exposes `launch()` which calls `FB.login`; when Meta returns an authorization `code`, the
 *    captured ids are combined with it and handed to `onComplete` for the backend exchange.
 */
export function useWhatsAppEmbeddedSignup({ appId, configId, onComplete }: UseWhatsAppEmbeddedSignupOptions) {
  const sessionInfoRef = useRef<SessionInfo>({ wabaId: null, phoneNumberId: null });
  const [isSdkReady, setIsSdkReady] = useState(false);
  const [isLaunching, setIsLaunching] = useState(false);

  useEffect(() => {
    if (!appId) {
      return;
    }

    let isMounted = true;
    loadFacebookSdk(appId)
      .then(() => {
        if (isMounted) {
          setIsSdkReady(true);
        }
      })
      .catch(() => {
        // The connect button stays disabled while the SDK is unavailable; nothing else to do.
      });

    return () => {
      isMounted = false;
    };
  }, [appId]);

  useEffect(() => {
    function handleMessage(event: MessageEvent) {
      if (!isFacebookOrigin(event.origin)) {
        return;
      }

      try {
        const message = JSON.parse(event.data);
        if (message?.type !== "WA_EMBEDDED_SIGNUP") {
          return;
        }

        const data = message.data ?? {};
        if (data.waba_id || data.phone_number_id) {
          sessionInfoRef.current = {
            wabaId: data.waba_id ?? sessionInfoRef.current.wabaId,
            phoneNumberId: data.phone_number_id ?? sessionInfoRef.current.phoneNumberId
          };
        }
      } catch {
        // Ignore non-JSON postMessage traffic from other integrations on the page.
      }
    }

    window.addEventListener("message", handleMessage);
    return () => window.removeEventListener("message", handleMessage);
  }, []);

  const launch = useCallback(() => {
    if (!window.FB) {
      return;
    }

    setIsLaunching(true);
    window.FB.login(
      (response) => {
        setIsLaunching(false);
        const code = response.authResponse?.code;
        const { wabaId, phoneNumberId } = sessionInfoRef.current;
        if (code && wabaId && phoneNumberId) {
          onComplete({ code, wabaId, phoneNumberId });
        }
      },
      {
        config_id: configId,
        response_type: "code",
        override_default_response_type: true,
        extras: { sessionInfoVersion: "3" }
      }
    );
  }, [configId, onComplete]);

  return { launch, isSdkReady, isLaunching };
}

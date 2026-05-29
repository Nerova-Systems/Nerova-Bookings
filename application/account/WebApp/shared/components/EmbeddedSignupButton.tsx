import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Spinner } from "@repo/ui/components/Spinner";
import { useCallback, useEffect, useRef, useState } from "react";

import { launchEmbeddedSignup, loadMetaSDK } from "@/shared/utils/metaSDK";

type EmbeddedSignupButtonProps = {
  onSuccess: (code: string) => void;
  onCancel?: () => void;
  onError?: (error: unknown) => void;
  disabled?: boolean;
  /** Optional pre-fill value for the Meta Embedded Signup business name field */
  businessName?: string;
};

/**
 * Renders a button that triggers the Meta Embedded Signup flow for
 * WhatsApp Business Account connection.
 *
 * The Meta JS SDK is loaded lazily on mount — the script tag is only
 * injected when this component renders.  A loading spinner is shown while
 * the SDK initialises.
 *
 * After the user completes the signup flow, `onSuccess` is called with the
 * OAuth authorisation `code`.  The parent component is responsible for
 * exchanging that code with the backend.
 *
 * Cancel (user closes the popup) calls `onCancel`.
 * Any SDK or network failure calls `onError`.
 */
export function EmbeddedSignupButton({
  onSuccess,
  onCancel,
  onError,
  disabled = false,
  businessName
}: Readonly<EmbeddedSignupButtonProps>) {
  const [isSdkReady, setIsSdkReady] = useState(false);
  const [sdkError, setSdkError] = useState(false);
  const [isLaunching, setIsLaunching] = useState(false);

  const appId = import.meta.runtime_env.PUBLIC_META_APP_ID;

  // Stable ref to onError — prevents the SDK load effect from re-firing on
  // every render when the parent passes an inline callback.
  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;

  useEffect(() => {
    if (!appId) {
      console.warn("EmbeddedSignupButton: PUBLIC_META_APP_ID is not configured or empty.");
      return;
    }

    console.log("EmbeddedSignupButton: Loading Meta JS SDK with App ID:", appId);
    loadMetaSDK(appId)
      .then(() => {
        console.log("EmbeddedSignupButton: Meta JS SDK loaded successfully!");
        setIsSdkReady(true);
      })
      .catch((error: unknown) => {
        console.error("EmbeddedSignupButton: Failed to load Meta JS SDK:", error);
        setSdkError(true);
        onErrorRef.current?.(error);
      });
  }, [appId]);

  const handleClick = useCallback(async () => {
    console.log("EmbeddedSignupButton: Clicked! isSdkReady:", isSdkReady, "appId:", appId);
    if (!appId || !isSdkReady) {
      console.warn("EmbeddedSignupButton: Cannot launch signup. App ID or SDK is not ready.");
      return;
    }

    setIsLaunching(true);
    try {
      console.log("EmbeddedSignupButton: Launching Meta Embedded Signup popup...");
      const response = await launchEmbeddedSignup({ appId, businessName });
      console.log("EmbeddedSignupButton: Meta Embedded Signup response:", response);
      if (response?.code) {
        onSuccess(response.code);
      } else {
        console.warn("EmbeddedSignupButton: Signup cancelled or returned no code.");
        onCancel?.();
      }
    } catch (error: unknown) {
      console.error("EmbeddedSignupButton: Error launching Embedded Signup:", error);
      onError?.(error);
    } finally {
      setIsLaunching(false);
    }
  }, [appId, businessName, isSdkReady, onCancel, onError, onSuccess]);

  const isSdkLoading = !isSdkReady && !sdkError && Boolean(appId);

  return (
    <Button
      onClick={() => void handleClick()}
      disabled={disabled || isSdkLoading || sdkError}
      isPending={isLaunching}
      aria-label={t`Connect WhatsApp Business Account via Meta Embedded Signup`}
    >
      {isSdkLoading ? (
        <>
          <Spinner className="size-4" />
          <Trans>Loading…</Trans>
        </>
      ) : (
        <Trans>Connect WhatsApp Business</Trans>
      )}
    </Button>
  );
}

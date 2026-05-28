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
    if (!appId) return;

    loadMetaSDK(appId)
      .then(() => {
        setIsSdkReady(true);
      })
      .catch((error: unknown) => {
        setSdkError(true);
        onErrorRef.current?.(error);
      });
  }, [appId]);

  const handleClick = useCallback(async () => {
    if (!appId || !isSdkReady) return;

    setIsLaunching(true);
    try {
      const response = await launchEmbeddedSignup({ appId, businessName });
      if (response?.code) {
        onSuccess(response.code);
      } else {
        onCancel?.();
      }
    } catch (error: unknown) {
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

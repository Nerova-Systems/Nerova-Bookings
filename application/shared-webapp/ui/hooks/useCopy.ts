import { useEffect, useState } from "react";

/**
 * Clipboard utilities with Safari-compatible `ClipboardItem` fallback.
 * Returns `{ isCopied, copyToClipboard, resetCopyStatus, fetchAndCopyToClipboard }`.
 * `isCopied` auto-resets to `false` after 3 seconds.
 *
 * Ported from cal.com `packages/lib/hooks/useCopy.ts` (cf2a55c).
 */
export function useCopy() {
  const [isCopied, setIsCopied] = useState(false);

  const noop = () => {};

  const copyToClipboard = (text: string, options: { onSuccess?: () => void; onFailure?: () => void } = {}) => {
    const { onSuccess = noop, onFailure = noop } = options;
    if (typeof navigator !== "undefined" && navigator.clipboard) {
      navigator.clipboard
        .writeText(text)
        .then(() => {
          setIsCopied(true);
          onSuccess();
        })
        .catch((error) => {
          onFailure();
          console.error("Copy to clipboard failed:", error);
        });
    } else {
      console.warn("Clipboard API requires a secure context. Text:", text);
      onFailure();
    }
  };

  const resetCopyStatus = () => {
    setIsCopied(false);
  };

  /** Safari-compatible async clipboard copy. @see https://wolfgangrittner.dev/how-to-use-clipboard-api-in-safari/ */
  const fetchAndCopyToClipboard = (
    promise: Promise<string>,
    options: { onSuccess?: () => void; onFailure?: () => void } = {}
  ) => {
    const { onSuccess = noop, onFailure = noop } = options;
    if (typeof ClipboardItem !== "undefined" && navigator.clipboard?.write) {
      const text = new ClipboardItem({
        "text/plain": promise
          .then((t) => new Blob([t], { type: "text/plain" }))
          .catch(() => {
            onFailure();
            return "";
          })
      });
      navigator.clipboard.write([text]);
      onSuccess();
    } else {
      promise.then((t) => copyToClipboard(t, options)).catch(() => onFailure());
    }
  };

  useEffect(() => {
    if (isCopied) {
      const timer = setTimeout(resetCopyStatus, 3000);
      return () => clearTimeout(timer);
    }
  }, [isCopied]);

  return { isCopied, copyToClipboard, resetCopyStatus, fetchAndCopyToClipboard };
}

import { useRef } from "react";

import { useIsomorphicLayoutEffect } from "./useIsomorphicLayoutEffect";

/**
 * Returns a stable ref whose `.current` is always the latest version of the callback.
 * Useful for event handlers that should not re-trigger effects when they change.
 *
 * Ported from cal.com `packages/lib/hooks/useCallbackRef.ts` (cf2a55c).
 */
export const useCallbackRef = <C>(callback: C): React.RefObject<C> => {
  const callbackRef = useRef(callback);

  useIsomorphicLayoutEffect(() => {
    callbackRef.current = callback;
  });

  return callbackRef;
};

export default useCallbackRef;

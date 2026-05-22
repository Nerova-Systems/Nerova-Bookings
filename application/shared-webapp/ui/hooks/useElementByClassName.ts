import type React from "react";

import { useEffect, useRef } from "react";

/**
 * Returns a ref that is populated on mount with the first DOM element matching
 * the given CSS `className`. Re-queries when `className` changes.
 *
 * Ported from cal.com `packages/lib/hooks/useElementByClassName.ts` (cf2a55c).
 */
export function useElementByClassName<T extends HTMLElement = HTMLDivElement>(
  className?: string
): React.RefObject<T | null> {
  const elementRef = useRef<T | null>(null);

  useEffect(() => {
    if (className) {
      elementRef.current = (document.getElementsByClassName(className)[0] as T) ?? null;
    } else {
      elementRef.current = null;
    }
  }, [className]);

  return elementRef;
}

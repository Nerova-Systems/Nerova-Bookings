import type React from "react";
import { useEffect } from "react";

/**
 * Calls `handler` when a click or touchstart event occurs outside the element
 * referenced by `ref`.
 *
 * Ported from cal.com `packages/lib/hooks/useOnclickOutside.ts` (cf2a55c).
 */
export default function useOnClickOutside(
  ref: React.RefObject<HTMLElement | null>,
  handler: (e?: MouseEvent | TouchEvent) => void
): void {
  useEffect(() => {
    const listener = (event: MouseEvent | TouchEvent) => {
      if (!ref.current || ref.current.contains(event.target as Node)) {
        return;
      }
      handler(event);
    };

    document.addEventListener("mousedown", listener);
    document.addEventListener("touchstart", listener);

    return () => {
      document.removeEventListener("mousedown", listener);
      document.removeEventListener("touchstart", listener);
    };
  }, [ref, handler]);
}

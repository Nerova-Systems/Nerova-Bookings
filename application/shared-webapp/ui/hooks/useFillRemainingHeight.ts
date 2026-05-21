import { useLayoutEffect, useRef } from "react";

/**
 * Returns a ref to attach to an element. Sets the element's `height` to fill
 * the remaining viewport height below its top edge, minus an optional `offset`.
 * Recomputes on body resize via `ResizeObserver`.
 *
 * Ported from cal.com `packages/lib/hooks/useFillRemainingHeight.ts` (cf2a55c).
 */
export function useFillRemainingHeight<T extends HTMLElement = HTMLDivElement>(
  offset = 0
): React.RefObject<T | null> {
  const ref = useRef<T>(null);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;

    const update = () => {
      const top = Math.round(el.getBoundingClientRect().top + offset);
      const newHeight = `calc(100dvh - ${top}px)`;
      if (el.style.height !== newHeight) {
        el.style.height = newHeight;
      }
    };

    update();

    const observer = new ResizeObserver(update);
    observer.observe(document.body);

    return () => observer.disconnect();
  }, [offset]);

  return ref;
}

import { useEffect, useRef, useState } from "react";

const isIntersectionObserverSupported = typeof window !== "undefined" && "IntersectionObserver" in window;

/**
 * Returns a ref-setter. When the attached element enters the viewport (or a custom
 * `root`), calls `onInViewCallback`. Uses `IntersectionObserver` under the hood.
 *
 * Ported from cal.com `packages/lib/hooks/useInViewObserver.ts` (cf2a55c).
 */
export const useInViewObserver = (
  onInViewCallback: () => void,
  root?: Element | Document | null
): { ref: (node: HTMLElement | null) => void } => {
  const [node, setRef] = useState<HTMLElement | null>(null);
  const onInViewCallbackRef = useRef(onInViewCallback);
  onInViewCallbackRef.current = onInViewCallback;

  useEffect(() => {
    if (!isIntersectionObserverSupported) return;

    let observer: IntersectionObserver;

    if (node?.parentElement) {
      observer = new IntersectionObserver(
        ([entry]) => {
          if (entry.isIntersecting) {
            onInViewCallbackRef.current();
          }
        },
        {
          root: root !== undefined ? root : document.body
        }
      );
      observer.observe(node);
    }

    return () => {
      observer?.disconnect();
    };
  }, [node, root]);

  return { ref: setRef };
};

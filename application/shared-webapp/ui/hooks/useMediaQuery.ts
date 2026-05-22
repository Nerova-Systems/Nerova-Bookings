import { useCallback, useSyncExternalStore } from "react";

/**
 * SSR-safe hook that returns whether a CSS media query is currently matched.
 * On the server (or when `window` is undefined) returns `false`.
 *
 * Ported from cal.com `packages/lib/hooks/useMediaQuery.ts` (cf2a55c).
 *
 * Deviation from Nerova `useViewportResize`: that hook returns a boolean `isMobile`
 * for a single breakpoint. This hook accepts any arbitrary CSS media query string,
 * making it a lower-level primitive. Prefer `useViewportResize` for mobile detection.
 */
const useMediaQuery = (query: string): boolean => {
  const subscribe = useCallback(
    (callback: () => void) => {
      const media = window.matchMedia(query);
      media.addEventListener("change", callback);
      return () => media.removeEventListener("change", callback);
    },
    [query]
  );

  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);

  // Return false during SSR to avoid hydration mismatches.
  const getServerSnapshot = () => false as boolean | undefined;

  const matches = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
  return matches ?? false;
};

export default useMediaQuery;

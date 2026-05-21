/**
 * Returns `true` when the app is running as a PWA in standalone mode.
 * Detects via the `?standalone=true` query parameter (set by the service worker
 * or manifest launch URL) as well as the `display-mode: standalone` media query.
 *
 * Ported from cal.com `packages/lib/hooks/useIsStandalone.ts` (cf2a55c).
 *
 * Deviation: cal.com reads from Next.js `useSearchParams`. Nerova uses browser
 * APIs directly since the project uses TanStack Router (no Next.js runtime).
 */
export const useIsStandalone = (): boolean => {
  if (typeof window === "undefined") return false;

  const standalone =
    new URLSearchParams(window.location.search).get("standalone") === "true" ||
    window.matchMedia("(display-mode: standalone)").matches;

  return standalone;
};

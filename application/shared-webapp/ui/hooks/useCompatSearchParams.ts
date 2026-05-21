import { useSyncExternalStore } from "react";

function getSnapshot() {
  return new URLSearchParams(window.location.search);
}

function subscribe(callback: () => void) {
  window.addEventListener("popstate", callback);
  return () => window.removeEventListener("popstate", callback);
}

/**
 * Returns the current URL search params as a `URLSearchParams` object,
 * reactive to navigation events.
 *
 * Ported from cal.com `packages/lib/hooks/useCompatSearchParams.ts` (cf2a55c).
 *
 * Deviation: cal.com reads from Next.js `useSearchParams` + `next/compat/router`.
 * Nerova uses TanStack Router (no Next.js runtime). This hook uses the browser
 * `URLSearchParams` API directly, which is router-agnostic.
 */
export function useCompatSearchParams(): URLSearchParams {
  return useSyncExternalStore(subscribe, getSnapshot, () => new URLSearchParams());
}

import { useSyncExternalStore } from "react";

function getSnapshot() {
  return window.location.pathname + window.location.search;
}

function subscribe(callback: () => void) {
  window.addEventListener("popstate", callback);
  // Listen for programmatic pushState / replaceState changes.
  const origPush = history.pushState.bind(history);
  const origReplace = history.replaceState.bind(history);
  history.pushState = (...args) => {
    origPush(...args);
    callback();
  };
  history.replaceState = (...args) => {
    origReplace(...args);
    callback();
  };
  return () => {
    window.removeEventListener("popstate", callback);
    history.pushState = origPush;
    history.replaceState = origReplace;
  };
}

/**
 * Returns the current `pathname + search` string, reactive to navigation events.
 *
 * Ported from cal.com `packages/lib/hooks/useAsPath.ts` (cf2a55c).
 *
 * Deviation: cal.com reads from `next/navigation`. Nerova uses TanStack Router
 * (no Next.js runtime). This hook uses the browser History API directly, which
 * is compatible with all SPA routing libraries.
 */
export function useAsPath(): string {
  return useSyncExternalStore(subscribe, getSnapshot, () => "");
}

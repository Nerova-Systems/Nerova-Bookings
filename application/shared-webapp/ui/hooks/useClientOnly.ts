import { useEffect } from "react";

/**
 * Runs a callback once after hydration, on the client only.
 * Use this to defer browser-only logic (e.g. reading localStorage) and avoid
 * SSR hydration mismatches.
 *
 * Ported from cal.com `packages/lib/hooks/useClientOnly.ts` (cf2a55c).
 */
export function useClientOnly(callback: () => void): void {
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(callback, []);
}

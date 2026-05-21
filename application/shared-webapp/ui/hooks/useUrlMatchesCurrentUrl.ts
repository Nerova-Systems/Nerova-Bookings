import { useCompatSearchParams } from "./useCompatSearchParams";

/**
 * Returns whether the provided `url` matches the current browser URL, checking
 * both pathname and search params.
 *
 * Ported from cal.com `packages/lib/hooks/useUrlMatchesCurrentUrl.ts` (cf2a55c).
 *
 * Deviation: cal.com uses Next.js `useSearchParams` + `usePathname`. Nerova uses
 * browser APIs directly.
 */
export function useUrlMatchesCurrentUrl(url: string): boolean {
  const currentSearchParams = useCompatSearchParams();

  try {
    // Resolve against the current origin so relative paths work.
    const target = new URL(url, window.location.origin);
    const targetParams = target.searchParams;

    // Pathname must match.
    if (target.pathname !== window.location.pathname) return false;

    // Every search param in `url` must also be present in the current URL.
    for (const [key, value] of targetParams.entries()) {
      if (currentSearchParams.get(key) !== value) return false;
    }

    return true;
  } catch {
    return false;
  }
}

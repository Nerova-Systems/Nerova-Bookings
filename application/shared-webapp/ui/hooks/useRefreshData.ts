/**
 * Triggers a full page re-fetch by reloading the current URL, equivalent to
 * calling `router.refresh()` in Next.js App Router.
 *
 * Ported from cal.com `packages/lib/hooks/useRefreshData.ts` (cf2a55c).
 *
 * Deviation: cal.com uses `router.refresh()` from Next.js App Router. Nerova uses
 * TanStack Router (no Next.js runtime). This hook returns a plain function that
 * invalidates the TanStack Router cache by reloading the window. For TanStack
 * Router-aware refresh (without full reload), pass a `router.invalidate()` callback
 * from the consuming component.
 */
export function useRefreshData(refreshFn?: () => void | Promise<void>) {
  return function refreshData(): void {
    if (refreshFn) {
      void refreshFn();
    } else {
      window.location.reload();
    }
  };
}

import { useAsPath } from "./useAsPath";
import { useCompatSearchParams } from "./useCompatSearchParams";

/**
 * Returns the current path and a URL matcher helper.
 * Useful for building breadcrumb navigation and active-link detection.
 *
 * Ported from cal.com `packages/lib/hooks/useParamsWithFallback.ts` (cf2a55c).
 *
 * Deviation: cal.com uses Next.js `usePathname` + `next/compat/router`. Nerova
 * uses browser APIs directly. The `useUrlMatchesCurrentUrl` hook is the preferred
 * way to check if a URL matches the current route.
 */
export function useParamsWithFallback(): { pathname: string; query: Record<string, string> } {
  const asPath = useAsPath();
  const searchParams = useCompatSearchParams();

  const query: Record<string, string> = {};
  searchParams.forEach((value, key) => {
    query[key] = value;
  });

  // Strip search string from asPath to get pure pathname.
  const pathname = asPath.split("?")[0] ?? asPath;

  return { pathname, query };
}

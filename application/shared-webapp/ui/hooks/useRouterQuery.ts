import { useCompatSearchParams } from "./useCompatSearchParams";

/**
 * Returns a record of all URL query parameters, falling back to an empty record
 * when no matching query string is present.
 *
 * Ported from cal.com `packages/lib/hooks/useRouterQuery.ts` (cf2a55c).
 *
 * Deviation: cal.com reads from Next.js `useRouter().query`. Nerova uses the browser
 * `URLSearchParams` API directly via `useCompatSearchParams`.
 */
export function useRouterQuery(): Record<string, string> {
  const searchParams = useCompatSearchParams();
  const result: Record<string, string> = {};
  searchParams.forEach((value, key) => {
    result[key] = value;
  });
  return result;
}

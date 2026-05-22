import { useMemo, useState } from "react";

export interface PaginationState {
  pageIndex: number;
  pageSize: number;
}

/**
 * Headless pagination state hook. Returns `{ pagination, setPagination }` where
 * `pagination` is memoised to be reference-stable unless the values change.
 *
 * Ported from cal.com `packages/lib/hooks/usePagination.ts` (cf2a55c).
 *
 * Deviation: cal.com imports `PaginationState` from `@tanstack/react-table`. Nerova
 * defines its own identical type here to avoid a hard dependency on react-table in
 * the shared UI package. The returned shape is compatible with `@tanstack/react-table`
 * should consumers pass it through.
 */
export function usePagination({
  defaultPageIndex = 1,
  defaultPageSize = 20
}: {
  defaultPageIndex?: number;
  defaultPageSize?: number;
} = {}) {
  const [{ pageIndex, pageSize }, setPagination] = useState<PaginationState>({
    pageIndex: defaultPageIndex,
    pageSize: defaultPageSize
  });

  const pagination = useMemo(() => ({ pageIndex, pageSize }), [pageIndex, pageSize]);

  return { pagination, setPagination };
}

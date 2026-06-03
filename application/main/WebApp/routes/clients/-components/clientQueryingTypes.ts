import type { SortableClientProperties, SortOrder } from "@/shared/lib/api/client";

export interface SearchParams {
  search: string | undefined;
  startDate: string | undefined;
  endDate: string | undefined;
  orderBy: SortableClientProperties | undefined;
  sortOrder: SortOrder | undefined;
  pageOffset: number | undefined;
}

export type FilterUpdateFn = (params: Partial<SearchParams>, isSearchUpdate?: boolean) => void;

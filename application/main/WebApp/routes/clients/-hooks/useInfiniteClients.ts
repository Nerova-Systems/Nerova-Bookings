import { useCallback, useEffect, useState } from "react";

import {
  api,
  apiClient,
  type components,
  type SortableClientProperties,
  type SortOrder
} from "@/shared/lib/api/client";

type ClientDetails = components["schemas"]["ClientDetails"];

interface UseInfiniteClientsParams {
  search?: string;
  startDate?: string;
  endDate?: string;
  orderBy?: SortableClientProperties;
  sortOrder?: SortOrder;
  enabled: boolean;
}

export function useInfiniteClients({
  search,
  startDate,
  endDate,
  orderBy,
  sortOrder,
  enabled
}: UseInfiniteClientsParams) {
  const [allClients, setAllClients] = useState<ClientDetails[]>([]);
  const [currentPage, setCurrentPage] = useState(0);
  const [totalPages, setTotalPages] = useState<number | null>(null);
  const [isLoadingMore, setIsLoadingMore] = useState(false);

  const { data: initialData, isLoading: isInitialLoading } = api.useQuery(
    "get",
    "/api/main/clients",
    {
      params: {
        query: {
          Search: search,
          StartDate: startDate,
          EndDate: endDate,
          OrderBy: orderBy,
          SortOrder: sortOrder
        }
      }
    },
    { enabled, refetchOnWindowFocus: true, refetchInterval: 15000 }
  );

  useEffect(() => {
    if (enabled && initialData) {
      setAllClients(initialData.clients || []);
      setTotalPages(initialData.totalPages || 1);
      setCurrentPage(0);
    }
  }, [enabled, initialData]);

  const loadMore = useCallback(async () => {
    if (isLoadingMore || !totalPages || currentPage >= totalPages - 1) {
      return;
    }

    const nextPage = currentPage + 1;
    setIsLoadingMore(true);

    try {
      const { data } = await apiClient.GET("/api/main/clients", {
        params: {
          query: {
            Search: search,
            StartDate: startDate,
            EndDate: endDate,
            OrderBy: orderBy,
            SortOrder: sortOrder,
            PageOffset: nextPage
          }
        }
      });

      if (data) {
        setAllClients((prev) => [...prev, ...(data.clients || [])]);
        setCurrentPage(nextPage);
      }
    } finally {
      setIsLoadingMore(false);
    }
  }, [currentPage, totalPages, isLoadingMore, search, startDate, endDate, orderBy, sortOrder]);

  const hasMore = totalPages !== null && currentPage < totalPages - 1;

  return {
    clients: allClients,
    isLoading: isInitialLoading,
    isLoadingMore,
    hasMore,
    loadMore,
    totalPages
  };
}

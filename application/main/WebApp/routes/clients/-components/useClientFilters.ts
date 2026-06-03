import { trackInteraction } from "@repo/infrastructure/applicationInsights/ApplicationInsightsProvider";
import { useDebounce } from "@repo/ui/hooks/useDebounce";
import { useLocation, useNavigate } from "@tanstack/react-router";
import { useCallback, useEffect, useState } from "react";

import type { SearchParams } from "./clientQueryingTypes";

interface UseClientFiltersOptions {
  onFiltersUpdated?: () => void;
}

export function useClientFilters({ onFiltersUpdated }: UseClientFiltersOptions = {}) {
  const navigate = useNavigate();
  const searchParams = (useLocation().search as SearchParams) ?? {};
  const [search, setSearch] = useState(searchParams.search ?? "");
  const debouncedSearch = useDebounce(search, 500);

  const dateRange =
    searchParams.startDate && searchParams.endDate
      ? { start: new Date(searchParams.startDate), end: new Date(searchParams.endDate) }
      : null;

  const updateFilter = useCallback(
    (params: Partial<SearchParams>, isSearchUpdate = false) => {
      navigate({
        to: "/clients",
        search: (prev) => ({ ...prev, ...params, pageOffset: undefined })
      });
      if (!isSearchUpdate) {
        onFiltersUpdated?.();
      }
    },
    [navigate, onFiltersUpdated]
  );

  useEffect(() => {
    navigate({
      to: "/clients",
      search: (prev) => ({ ...prev, search: debouncedSearch || undefined, pageOffset: undefined })
    });
  }, [debouncedSearch, navigate]);

  const activeFilterCount = searchParams.startDate && searchParams.endDate ? 1 : 0;

  const clearAllFilters = () => {
    trackInteraction("Client filters", "interaction", "Clear");
    setSearch("");
    updateFilter({ search: undefined, startDate: undefined, endDate: undefined });
  };

  return {
    search,
    setSearch,
    searchParams,
    dateRange,
    activeFilterCount,
    updateFilter,
    clearAllFilters
  };
}

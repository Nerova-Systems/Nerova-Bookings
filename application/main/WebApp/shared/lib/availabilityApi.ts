import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useQueries } from "@tanstack/react-query";

import type { Slot } from "./appointmentContracts";

export function useWeekAvailabilitySlots(serviceId: string | undefined, dates: string[]) {
  const queries = useQueries({
    queries: dates.map((date) => ({
      queryKey: ["availability-slots", serviceId, date],
      queryFn: () => fetchAvailabilitySlots(serviceId!, date),
      enabled: Boolean(serviceId)
    }))
  });

  return {
    slots: queries.flatMap((query) => query.data ?? []),
    isLoading: queries.some((query) => query.isLoading)
  };
}

async function fetchAvailabilitySlots(serviceId: string, date: string): Promise<Slot[]> {
  const params = new URLSearchParams({ serviceId, date });
  const response = await enhancedFetch(`/api/main/app/availability/slots?${params.toString()}`);
  if (!response.ok) throw new Error(await response.text());
  return (await response.json()) as Slot[];
}

import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import type { ApiShell } from "./appointmentContracts";
import { mapShell } from "./appointmentMappers";

export interface AvailabilityWindowMutationRequest {
  startTime: string;
  endTime: string;
}

export interface AvailabilityDayMutationRequest {
  dayOfWeek: string;
  windows: AvailabilityWindowMutationRequest[];
}

export interface WeeklyAvailabilityMutationRequest {
  days: AvailabilityDayMutationRequest[];
}

export interface ClosureMutationRequest {
  startDate: string;
  endDate: string;
  label: string;
}

export interface HolidaySettingsMutationRequest {
  countryCode: string;
  openHolidayIds: string[];
}

export function useUpdateWeeklyAvailability() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: WeeklyAvailabilityMutationRequest) => {
      const response = await enhancedFetch("/api/main/app/availability/weekly", {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: async (shell) => {
      queryClient.setQueryData(["appointment-shell"], shell);
      await queryClient.invalidateQueries({ queryKey: ["availability-slots"] });
    }
  });
}

export function useUpdateHolidaySettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: HolidaySettingsMutationRequest) => {
      const response = await enhancedFetch("/api/main/app/availability/holidays", {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: async (shell) => {
      queryClient.setQueryData(["appointment-shell"], shell);
      await queryClient.invalidateQueries({ queryKey: ["availability-slots"] });
    }
  });
}

export function useCreateClosure() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: ClosureMutationRequest) => {
      const response = await enhancedFetch("/api/main/app/availability/closures", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: async (shell) => {
      queryClient.setQueryData(["appointment-shell"], shell);
      await queryClient.invalidateQueries({ queryKey: ["availability-slots"] });
    }
  });
}

export function useDeleteClosure() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const response = await enhancedFetch(`/api/main/app/availability/closures/${id}`, { method: "DELETE" });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: async (shell) => {
      queryClient.setQueryData(["appointment-shell"], shell);
      await queryClient.invalidateQueries({ queryKey: ["availability-slots"] });
    }
  });
}

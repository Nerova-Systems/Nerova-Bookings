import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export interface HolidayCountry {
  code: string;
  name: string;
}

export interface PublicHoliday {
  id: string;
  countryCode: string;
  date: string;
  label: string;
  isOpen: boolean;
}

export interface HolidaySettings {
  countryCode: string;
  countries: HolidayCountry[];
  holidays: PublicHoliday[];
}

export interface BusinessClosure {
  id: string;
  startDate: string;
  endDate: string;
  label: string;
  type: "manual" | "publicHoliday";
}

export interface IntegrationConnection {
  provider: string;
  capability: string;
  status: string;
  lastSyncedAt?: string;
  externalConnectionId?: string | null;
}

export interface MainAppointmentShell {
  profile?: {
    name: string;
    slug: string;
    timeZone: string;
    address: string;
    publicBookingEnabled: boolean;
  };
  integrations: IntegrationConnection[];
  holidaySettings?: HolidaySettings;
  closures: BusinessClosure[];
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

const mainShellQueryKey = ["main-appointment-shell"];

export function useMainAppointmentShell() {
  return useQuery({
    queryKey: mainShellQueryKey,
    queryFn: async () => {
      const response = await enhancedFetch("/api/main/app/shell");
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as MainAppointmentShell;
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
      return (await response.json()) as MainAppointmentShell;
    },
    onSuccess: async (shell) => {
      queryClient.setQueryData(mainShellQueryKey, shell);
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
      return (await response.json()) as MainAppointmentShell;
    },
    onSuccess: async (shell) => {
      queryClient.setQueryData(mainShellQueryKey, shell);
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
      return (await response.json()) as MainAppointmentShell;
    },
    onSuccess: async (shell) => {
      queryClient.setQueryData(mainShellQueryKey, shell);
      await queryClient.invalidateQueries({ queryKey: ["availability-slots"] });
    }
  });
}

export function useCreateIntegrationConnectSession() {
  return useMutation({
    mutationFn: async (request: { appSlug: string }) => {
      const response = await enhancedFetch("/api/main/integrations/connect-session", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as { connectLink: string; expiresAt: string; integrationKey: string };
    }
  });
}

export function useSyncIntegrationConnections() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: { appSlug: string }) => {
      const response = await enhancedFetch("/api/main/integrations/sync-connections", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as IntegrationConnection[];
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: mainShellQueryKey });
    }
  });
}

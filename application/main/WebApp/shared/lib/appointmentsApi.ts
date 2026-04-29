import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useMutation, useQuery } from "@tanstack/react-query";

import type { ApiShell } from "./appointmentContracts";

import { mapShell, money } from "./appointmentMappers";

export type {
  Analytics,
  Appointment,
  AppointmentShell,
  AppointmentStatus,
  Client,
  IntegrationConnection,
  Service,
  ServiceCategory
} from "./appointmentContracts";
export { money };

export function useAppointmentShell() {
  return useQuery({
    queryKey: ["appointment-shell"],
    queryFn: async () => {
      const response = await enhancedFetch("/api/main/app/shell");
      return mapShell((await response.json()) as ApiShell);
    }
  });
}

export function useConfirmAppointment() {
  return useMutation({
    mutationFn: async (id: string) => {
      const response = await enhancedFetch(`/api/main/app/appointments/${id}/confirm`, { method: "POST" });
      return mapShell((await response.json()) as ApiShell);
    }
  });
}

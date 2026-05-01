import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type { ApiShell, ServicePaymentPolicy } from "./appointmentContracts";

import { mapShell, money } from "./appointmentMappers";

export type {
  Analytics,
  Appointment,
  AppointmentShell,
  AppointmentStatus,
  AvailabilityRule,
  BusinessClosure,
  CalendarBlock,
  Client,
  HolidaySettings,
  IntegrationConnection,
  PublicHoliday,
  Service,
  ServiceCategory,
  Slot,
  ServicePaymentPolicy
} from "./appointmentContracts";
export { money };

export interface ServiceMutationRequest {
  name: string;
  categoryName: string;
  description?: string;
  mode: "physical" | "virtual" | "mobile";
  durationMinutes: number;
  priceCents: number;
  depositCents: number;
  paymentPolicy: ServicePaymentPolicy;
  bufferBeforeMinutes: number;
  bufferAfterMinutes: number;
  location: string;
}

export interface CalendarBlockMutationRequest {
  title: string;
  startAt: string;
  endAt: string;
  staffMemberId?: string;
}

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
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const response = await enhancedFetch(`/api/main/app/appointments/${id}/confirm`, { method: "POST" });
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: (shell) => queryClient.setQueryData(["appointment-shell"], shell)
  });
}

export function useCreateCalendarBlock() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: CalendarBlockMutationRequest) => {
      const response = await enhancedFetch("/api/main/app/calendar/blocks", {
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

export function useUpdateAppointmentStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, status, paymentStatus }: { id: string; status: string; paymentStatus?: string }) => {
      const response = await enhancedFetch(`/api/main/app/appointments/${id}/status`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ status, paymentStatus })
      });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: (shell) => queryClient.setQueryData(["appointment-shell"], shell)
  });
}

export interface TerminalPaymentIntent {
  reference: string;
  amountCents: number;
  status: string;
  virtualTerminalCode: string;
  terminalUrl: string;
}

export function useCreateTerminalPaymentIntent() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const response = await enhancedFetch(`/api/main/app/appointments/${id}/payments/terminal-intent`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({})
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as TerminalPaymentIntent;
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["appointment-shell"] }),
        queryClient.invalidateQueries({ queryKey: ["payment-overview"] })
      ]);
    }
  });
}

export function useCreateService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: ServiceMutationRequest) => {
      const response = await enhancedFetch("/api/main/app/services", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: (shell) => queryClient.setQueryData(["appointment-shell"], shell)
  });
}

export function useUpdateService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, request }: { id: string; request: ServiceMutationRequest }) => {
      const response = await enhancedFetch(`/api/main/app/services/${id}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: (shell) => queryClient.setQueryData(["appointment-shell"], shell)
  });
}

export function useArchiveService() {
  return useServiceStateMutation("archive");
}

export function useRestoreService() {
  return useServiceStateMutation("restore");
}

export function useUpdateClient() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      request
    }: {
      id: string;
      request: { name: string; phone: string; email: string; status: string; alert?: string; internalNote?: string };
    }) => {
      const response = await enhancedFetch(`/api/main/app/clients/${id}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: (shell) => queryClient.setQueryData(["appointment-shell"], shell)
  });
}

function useServiceStateMutation(action: "archive" | "restore") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const response = await enhancedFetch(`/api/main/app/services/${id}/${action}`, { method: "POST" });
      if (!response.ok) throw new Error(await response.text());
      return mapShell((await response.json()) as ApiShell);
    },
    onSuccess: (shell) => queryClient.setQueryData(["appointment-shell"], shell)
  });
}

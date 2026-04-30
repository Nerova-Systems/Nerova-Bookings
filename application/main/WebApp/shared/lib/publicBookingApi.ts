import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useMutation, useQuery } from "@tanstack/react-query";

import type { ServicePaymentPolicy } from "./appointmentContracts";

export interface PublicBookingService {
  id: string;
  name: string;
  mode: string;
  durationMinutes: number;
  priceCents: number;
  depositCents: number;
  paymentPolicy: ServicePaymentPolicy;
  location: string;
  isActive: boolean;
}

export interface PublicBookingProfile {
  name: string;
  slug: string;
  timeZone: string;
  address: string;
  logoUrl?: string;
  services: PublicBookingService[];
}

export interface PublicClientPrefill {
  name: string;
  email: string;
}

export interface Slot {
  startAt: string;
  endAt: string;
}

export interface PublicBookingCreated {
  reference: string;
  paymentRequired: boolean;
  paymentUrl?: string;
}

export interface PublicConfirmation {
  id: string;
  publicReference: string;
  startAt: string;
  endAt: string;
  clientName: string;
  serviceName: string;
  priceCents: number;
  depositCents: number;
  status: string;
  paymentStatus: string;
  location: string;
}

export function usePublicBookingProfile(businessSlug: string) {
  return useQuery({
    queryKey: ["public-booking-profile", businessSlug],
    queryFn: async () => {
      const response = await enhancedFetch(`/api/main/public-booking/${businessSlug}`);
      return (await response.json()) as PublicBookingProfile;
    }
  });
}

export function usePublicSlots(businessSlug: string, serviceId: string | undefined, date: string) {
  return useQuery({
    enabled: Boolean(serviceId),
    queryKey: ["public-booking-slots", businessSlug, serviceId, date],
    queryFn: async () => {
      const response = await enhancedFetch(
        `/api/main/public-booking/${businessSlug}/slots?serviceId=${serviceId}&date=${date}`
      );
      return (await response.json()) as Slot[];
    }
  });
}

export function usePublicClientPrefill(businessSlug: string, phone: string) {
  const normalizedPhone = phone.replace(/[^\d+]/g, "");
  return useQuery({
    enabled: normalizedPhone.length >= 8,
    queryKey: ["public-booking-client-prefill", businessSlug, normalizedPhone],
    queryFn: async () => {
      const response = await enhancedFetch(
        `/api/main/public-booking/${businessSlug}/client-prefill?phone=${encodeURIComponent(normalizedPhone)}`
      );
      return (await response.json()) as PublicClientPrefill;
    }
  });
}

export function useCreatePublicBooking(businessSlug: string) {
  return useMutation({
    mutationFn: async (request: {
      serviceId: string;
      startAt: string;
      name: string;
      phone: string;
      email: string;
      answers: Record<string, string>;
    }) => {
      const response = await enhancedFetch(`/api/main/public-booking/${businessSlug}/appointments`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as PublicBookingCreated;
    }
  });
}

export function usePublicConfirmation(reference: string) {
  return useQuery({
    queryKey: ["public-booking-confirmation", reference],
    queryFn: async () => {
      const response = await enhancedFetch(`/api/main/public-booking/confirmation/${reference}`);
      return (await response.json()) as PublicConfirmation;
    }
  });
}

export function useConfirmPaystackReference(reference: string) {
  return useQuery({
    enabled: Boolean(reference),
    queryKey: ["paystack-confirm", reference],
    queryFn: async () => {
      const response = await enhancedFetch(`/api/main/payments/paystack/confirm?reference=${reference}`);
      return (await response.json()) as { appointmentReference: string; status: string };
    }
  });
}

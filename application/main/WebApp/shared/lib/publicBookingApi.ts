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
  latestVersionNumber: number;
}

export interface PublicBookingProfile {
  name: string;
  slug: string;
  timeZone: string;
  address: string;
  logoUrl?: string;
  services: PublicBookingService[];
}

export interface PublicPhoneVerificationStarted {
  maskedPhone: string;
  expiresAt: string;
  resendAfterSeconds: number;
}

export interface PublicPhoneVerificationChecked {
  phoneVerificationToken: string;
  maskedPhone: string;
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

export interface PublicPaymentBusiness {
  name: string;
  logoUrl?: string;
  brandColor: string;
}

export interface PublicPaymentAppointment {
  reference: string;
  serviceName: string;
  startAt: string;
  endAt: string;
  location: string;
}

export interface PublicPaymentAmount {
  amountCents: number;
  currency: string;
  status: string;
  expiresAt: string;
}

export interface PublicPaymentDetails {
  business: PublicPaymentBusiness;
  appointment: PublicPaymentAppointment;
  payment: PublicPaymentAmount;
}

export interface PublicPaymentInitialized {
  reference: string;
  accessCode: string;
  authorizationUrl: string;
  amountCents: number;
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

export interface PublicRescheduleApproval {
  appointment: PublicConfirmation;
  proposedStartAt: string;
  proposedEndAt: string;
  note: string;
  status: string;
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

export function useStartPublicPhoneVerification(businessSlug: string) {
  return useMutation({
    mutationFn: async (request: { phone: string }) => {
      const response = await enhancedFetch(`/api/main/public-booking/${businessSlug}/phone-verifications`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as PublicPhoneVerificationStarted;
    }
  });
}

export function useCheckPublicPhoneVerification(businessSlug: string) {
  return useMutation({
    mutationFn: async (request: { phone: string; code: string }) => {
      const response = await enhancedFetch(`/api/main/public-booking/${businessSlug}/phone-verifications/check`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as PublicPhoneVerificationChecked;
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
      phoneVerificationToken: string;
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

export function usePublicPaymentDetails(token: string) {
  return useQuery({
    enabled: Boolean(token),
    retry: false,
    queryKey: ["public-payment-details", token],
    queryFn: async () => {
      const response = await enhancedFetch(`/api/main/public/pay/${encodeURIComponent(token)}`);
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as PublicPaymentDetails;
    }
  });
}

export function useInitializePublicPayment(token: string) {
  return useMutation({
    mutationFn: async () => {
      const response = await enhancedFetch(`/api/main/public/pay/${encodeURIComponent(token)}/initialize`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({})
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as PublicPaymentInitialized;
    }
  });
}

export function usePublicRescheduleApproval(token: string) {
  return useQuery({
    queryKey: ["public-reschedule-approval", token],
    queryFn: async () => {
      const response = await enhancedFetch(`/api/main/public-booking/approvals/${token}`);
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as PublicRescheduleApproval;
    }
  });
}

export function useRespondToRescheduleApproval(token: string, decision: "approve" | "reject") {
  return useMutation({
    mutationFn: async () => {
      const response = await enhancedFetch(`/api/main/public-booking/approvals/${token}/${decision}`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({})
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as { appointmentReference: string; status: string };
    }
  });
}

export function useConfirmPaystackReference(reference: string) {
  return useQuery({
    enabled: Boolean(reference),
    queryKey: ["paystack-confirm", reference],
    queryFn: async () => {
      const response = await enhancedFetch(
        `/api/main/payments/paystack/confirm?reference=${encodeURIComponent(reference)}`
      );
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as { appointmentReference: string; status: string };
    }
  });
}

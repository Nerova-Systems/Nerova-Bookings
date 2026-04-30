import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { money } from "./appointmentsApi";

export interface PaymentOverview {
  stats: PaymentStats;
  subaccount?: PaystackSubaccount;
  recentPayments: PaymentIntent[];
}

export interface PaymentStats {
  totalTracked: number;
  paidOrConfirmed: number;
  needsAction: number;
  overdue: number;
  amountPendingCents: number;
  amountPaidCents: number;
}

export interface PaymentIntent {
  reference: string;
  amountCents: number;
  status: string;
  authorizationUrl?: string;
  createdAt: string;
  confirmedAt?: string;
  appointmentReference: string;
  clientName: string;
  serviceName: string;
}

export interface PaystackSubaccount {
  subaccountCode: string;
  splitCode?: string;
  virtualTerminalCode?: string;
  businessName: string;
  settlementBankName: string;
  settlementBankCode: string;
  accountName: string;
  maskedAccountNumber: string;
  currency: string;
  primaryContactName?: string;
  primaryContactEmail?: string;
  primaryContactPhone?: string;
  isActive: boolean;
  isVerified: boolean;
  settlementSchedule: string;
  lastSyncedAt: string;
}

export interface PaystackBank {
  name: string;
  code: string;
  currency: string;
  country: string;
}

export interface ResolvedPaystackAccount {
  bankCode: string;
  maskedAccountNumber: string;
  accountName: string;
}

export interface SavePaystackSubaccountRequest {
  bankName: string;
  bankCode: string;
  accountNumber: string;
  accountName: string;
  primaryContactName?: string;
  primaryContactEmail?: string;
  primaryContactPhone?: string;
}

export interface PaystackSettlement {
  id: string;
  status: string;
  totalAmountCents: number;
  effectiveAmountCents: number;
  feesCents: number;
  settlementDate?: string;
}

export function usePaymentOverview() {
  return useQuery({
    queryKey: ["payment-overview"],
    queryFn: async () => {
      const response = await enhancedFetch("/api/main/app/payments/overview");
      return (await response.json()) as PaymentOverview;
    }
  });
}

export function usePaystackBanks() {
  return useQuery({
    queryKey: ["paystack-banks"],
    queryFn: async () => {
      const response = await enhancedFetch("/api/main/app/payments/paystack/banks");
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as PaystackBank[];
    }
  });
}

export function useResolvePaystackAccount() {
  return useMutation({
    mutationFn: async (request: { bankCode: string; accountNumber: string }) => {
      const response = await enhancedFetch("/api/main/app/payments/paystack/resolve-account", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as ResolvedPaystackAccount;
    }
  });
}

export function useSavePaystackSubaccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: SavePaystackSubaccountRequest) => {
      const response = await enhancedFetch("/api/main/app/payments/paystack/subaccount", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as PaystackSubaccount;
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["payment-overview"] }),
        queryClient.invalidateQueries({ queryKey: ["appointment-shell"] }),
        queryClient.invalidateQueries({ queryKey: ["paystack-settlements"] })
      ]);
    }
  });
}

export function usePaystackSettlements(enabled: boolean) {
  return useQuery({
    enabled,
    queryKey: ["paystack-settlements"],
    queryFn: async () => {
      const response = await enhancedFetch("/api/main/app/payments/paystack/settlements");
      if (!response.ok) throw new Error(await response.text());
      return ((await response.json()) as { settlements: PaystackSettlement[] }).settlements;
    }
  });
}

export function paymentMoney(cents: number) {
  return money(cents);
}

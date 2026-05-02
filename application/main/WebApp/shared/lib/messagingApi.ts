import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export interface TenantMessagingStatus {
  appSlug: string;
  appName: string;
  provider: string;
  countryCode: string;
  status: string;
  whatsAppApprovalStatus: string;
  twilioSubaccountSid?: string;
  phoneNumber?: string;
  templateCount: number;
  canSendMessages: boolean;
  readiness: MessagingReadinessItem[];
}

export interface MessagingReadinessItem {
  key: string;
  label: string;
  isReady: boolean;
}

export function useWhatsAppMessagingStatus(enabled = true) {
  return useQuery({
    enabled,
    queryKey: ["whatsapp-messaging-status"],
    queryFn: async () => {
      const response = await enhancedFetch("/api/main/messaging/whatsapp/status");
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as TenantMessagingStatus;
    }
  });
}

export function useProvisionWhatsAppSubaccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const response = await enhancedFetch("/api/main/messaging/whatsapp/provision-subaccount", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({})
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as TenantMessagingStatus;
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["whatsapp-messaging-status"] });
    }
  });
}

export function useClaimWhatsAppNumber() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const response = await enhancedFetch("/api/main/messaging/whatsapp/claim-number", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({})
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as TenantMessagingStatus;
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["whatsapp-messaging-status"] });
    }
  });
}

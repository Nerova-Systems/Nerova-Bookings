import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import type { ApiShell } from "./appointmentContracts";

export interface IntegrationAppRequest {
  appSlug: string;
}

export interface ConnectSessionResponse {
  connectLink: string;
  expiresAt: string;
  integrationKey: string;
}

export function useCreateIntegrationConnectSession() {
  return useMutation({
    mutationFn: async (request: IntegrationAppRequest) => {
      const response = await enhancedFetch("/api/main/integrations/connect-session", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as ConnectSessionResponse;
    }
  });
}

export function useSyncIntegrationConnections() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: IntegrationAppRequest) => {
      const response = await enhancedFetch("/api/main/integrations/sync-connections", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(request)
      });
      if (!response.ok) throw new Error(await response.text());
      return (await response.json()) as ApiShell["integrations"];
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["appointment-shell"] });
    }
  });
}

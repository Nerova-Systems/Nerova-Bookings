import type { QueryClient } from "@tanstack/react-query";

import { toast } from "sonner";

export interface SsoMutationLike<TResult = unknown> {
  mutateAsync: (
    vars: Record<string, never>,
    opts?: { onSuccess?: (result: TResult) => void | Promise<void> }
  ) => Promise<TResult>;
  isPending: boolean;
}

export interface SsoTestResult {
  success: boolean;
  errorMessage: string | null;
}

export function parseDomains(input: string): string[] {
  return input
    .split(/[\s,]+/)
    .map((d) => d.trim())
    .filter((d) => d.length > 0);
}

export function invalidateSso(queryClient: QueryClient, queryKeyPrefix: string) {
  return queryClient.invalidateQueries({
    predicate: (query) =>
      Array.isArray(query.queryKey) &&
      typeof query.queryKey[1] === "string" &&
      query.queryKey[1].startsWith(queryKeyPrefix)
  });
}

export function buildSaveSuccess(queryClient: QueryClient, queryKeyPrefix: string, after: () => void) {
  return {
    onSuccess: async () => {
      await invalidateSso(queryClient, queryKeyPrefix);
      after();
    }
  };
}

export async function runSsoToggle(
  isEnabled: boolean,
  enableMutation: SsoMutationLike,
  disableMutation: SsoMutationLike,
  queryClient: QueryClient,
  queryKeyPrefix: string,
  messages: { enable: string; disable: string }
) {
  const mutation = isEnabled ? disableMutation : enableMutation;
  await mutation.mutateAsync({} as Record<string, never>, {
    onSuccess: async () => {
      await invalidateSso(queryClient, queryKeyPrefix);
      toast.success(isEnabled ? messages.disable : messages.enable);
    }
  });
}

export async function runSsoTest(
  testMutation: SsoMutationLike<SsoTestResult>,
  successMessage: string,
  fallbackError: string
) {
  await testMutation.mutateAsync({} as Record<string, never>, {
    onSuccess: (result) => {
      if (result.success) {
        toast.success(successMessage);
      } else {
        toast.error(result.errorMessage ?? fallbackError);
      }
    }
  });
}

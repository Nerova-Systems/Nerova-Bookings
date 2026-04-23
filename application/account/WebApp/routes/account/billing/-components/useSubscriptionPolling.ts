import { t } from "@lingui/core/macro";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

const PollIntervalMs = 1000;
const PollTimeoutMs = 15_000;

type SubscriptionData = Schemas["SubscriptionResponse"];

export function useSubscriptionPolling() {
  const queryClient = useQueryClient();
  const [isPolling, setIsPolling] = useState(false);
  const checkFnRef = useRef<((subscription: SubscriptionData) => boolean) | null>(null);
  const successMessageRef = useRef<string>("");
  const onCompleteRef = useRef<(() => void) | null>(null);
  const conditionMetRef = useRef(false);

  const { data: subscription, isLoading } = api.useQuery(
    "get",
    "/api/account/subscriptions/current",
    {},
    { refetchInterval: isPolling ? PollIntervalMs : false }
  );

  function startPolling(options: {
    check: (subscription: SubscriptionData) => boolean;
    successMessage: string;
    onComplete?: () => void;
  }) {
    checkFnRef.current = options.check;
    successMessageRef.current = options.successMessage;
    onCompleteRef.current = options.onComplete ?? null;
    conditionMetRef.current = false;
    setIsPolling(true);
    queryClient.invalidateQueries({ queryKey: ["get", "/api/account/subscriptions/current"] });
  }

  useEffect(() => {
    if (!isPolling || !subscription) {
      return;
    }
    if (checkFnRef.current?.(subscription)) {
      conditionMetRef.current = true;
      setIsPolling(false);
      queryClient.invalidateQueries();
      toast.success(successMessageRef.current);
      onCompleteRef.current?.();
    }
  }, [isPolling, subscription, queryClient]);

  useEffect(() => {
    if (!isPolling) {
      return;
    }
    const timeout = setTimeout(() => {
      if (!conditionMetRef.current) {
        setIsPolling(false);
        queryClient.invalidateQueries();
        toast.warning(t`Your changes may take a moment to appear.`);
        onCompleteRef.current?.();
      }
    }, PollTimeoutMs);
    return () => clearTimeout(timeout);
  }, [isPolling, queryClient]);

  return { isPolling, isLoading, startPolling, subscription };
}

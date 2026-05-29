import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { Button } from "@repo/ui/components/Button";
import { Link as RouterLink } from "@tanstack/react-router";
import { XIcon } from "lucide-react";
import { useState } from "react";

import { api } from "@/shared/lib/api/client";

const DISMISS_KEY = "brand-profile-nudge-dismissed-v1";

export function BrandProfileNudgeBanner() {
  const userInfo = useUserInfo();
  const isOwner = userInfo?.role === "Owner";

  const [dismissed, setDismissed] = useState(() => {
    try {
      return localStorage.getItem(DISMISS_KEY) === "1";
    } catch {
      return false;
    }
  });

  /**
   * Show nudge when WABA is linked but brand profile hasn't been confirmed.
   * TODO: Replace condition with `!brandProfile.businessDisplayName` once
   *       GET /api/account/tenants/current/brand-profile endpoint is available.
   */
  const { data: onboardingStatus } = api.useQuery(
    "get",
    "/api/whatsapp/onboarding-status",
    {},
    { enabled: isOwner && !dismissed }
  );

  const wabaLinked = onboardingStatus?.wabaLinked === true;

  if (!isOwner || !wabaLinked || dismissed) {
    return null;
  }

  const handleDismiss = () => {
    try {
      localStorage.setItem(DISMISS_KEY, "1");
    } catch {
      // ignore storage errors
    }
    setDismissed(true);
  };

  return (
    <div
      className="flex h-12 items-center gap-3 border-b bg-info/10 px-4 text-sm"
      role="status"
      aria-label={t`Brand profile nudge`}
    >
      <span className="flex-1 text-info-foreground">
        <Trans>Complete your brand profile to publish your WhatsApp flow.</Trans>
      </span>
      <Button variant="default" size="sm" render={<RouterLink to="/account/settings" />}>
        <Trans>Complete profile</Trans>
      </Button>
      <button
        type="button"
        onClick={handleDismiss}
        className="shrink-0 opacity-70 hover:opacity-100"
        aria-label={t`Dismiss brand profile banner`}
      >
        <XIcon className="size-4" />
      </button>
    </div>
  );
}

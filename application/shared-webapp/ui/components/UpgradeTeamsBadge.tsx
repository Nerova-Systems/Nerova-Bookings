import { Trans } from "@lingui/react/macro";
import { ArrowUpRightIcon } from "lucide-react";

import { cn } from "../utils";
import { Badge } from "./Badge";

/**
 * EE upsell badge shown next to features that require a Teams upgrade.
 * Ported from cal.com `packages/ui/components/badge/UpgradeTeamsBadge.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface UpgradeTeamsBadgeProps {
  /** Custom label. Defaults to "Teams". */
  label?: string;
  className?: string;
}

export function UpgradeTeamsBadge({ label, className }: UpgradeTeamsBadgeProps) {
  return (
    <Badge
      data-slot="upgrade-teams-badge"
      variant="secondary"
      className={cn("text-brand-900 dark:text-brand-400 gap-1", className)}
    >
      <ArrowUpRightIcon data-icon="inline-start" className="size-3" />
      {label ? label : <Trans>Teams</Trans>}
    </Badge>
  );
}

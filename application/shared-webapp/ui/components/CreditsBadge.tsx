import { Trans } from "@lingui/react/macro";
import { ZapIcon } from "lucide-react";

import { cn } from "../utils";
import { Badge } from "./Badge";

/**
 * Badge displaying an EE credit count with a lightning bolt icon.
 * Ported from cal.com `packages/ui/components/badge/CreditsBadge.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface CreditsBadgeProps {
  credits: number;
  className?: string;
}

export function CreditsBadge({ credits, className }: CreditsBadgeProps) {
  return (
    <Badge data-slot="credits-badge" variant="secondary" className={cn("gap-1 tabular-nums", className)}>
      <ZapIcon data-icon="inline-start" className="text-warning" />
      <Trans>{credits} credits</Trans>
    </Badge>
  );
}

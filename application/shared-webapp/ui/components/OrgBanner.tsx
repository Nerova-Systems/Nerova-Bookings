import { Trans } from "@lingui/react/macro";
import { InfoIcon } from "lucide-react";

import { cn } from "../utils";

/**
 * Informational banner shown when viewing as an organization context.
 * Ported from cal.com `packages/ui/components/organization-banner/OrgBanner.tsx` (cf2a55c).
 *
 * Note: TopBanner (ui-parity row 181) is a separate, general-purpose dismissible banner.
 * OrgBanner is a simpler, non-dismissible informational strip specifically for org context.
 *
 * No prop deviations.
 */
interface OrgBannerProps {
  /** Name of the organization being viewed. */
  orgName: string;
  /** URL or path to the organization's home. */
  orgHref?: string;
  className?: string;
}

export function OrgBanner({ orgName, orgHref, className }: OrgBannerProps) {
  return (
    <div
      data-slot="org-banner"
      className={cn(
        "flex items-center justify-center gap-2 bg-info/10 px-4 py-2 text-sm text-info-foreground",
        className
      )}
      role="status"
    >
      <InfoIcon className="size-4 shrink-0" />
      <span>
        {orgHref ? (
          <Trans>
            You are viewing{" "}
            <a href={orgHref} className="font-semibold underline underline-offset-2 hover:no-underline">
              {orgName}
            </a>
          </Trans>
        ) : (
          <Trans>
            You are viewing <span className="font-semibold">{orgName}</span>
          </Trans>
        )}
      </span>
    </div>
  );
}

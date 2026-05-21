import { Trans } from "@lingui/react/macro";
import { EyeOffIcon } from "lucide-react";

import { cn } from "../utils";

/**
 * Overlay for draft / disabled event types showing an "Unpublished" state.
 * Ported from cal.com `packages/ui/components/unpublished-entity/UnpublishedEntity.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface UnpublishedEntityProps {
  /** Name of the entity (event type name, page name, etc.). */
  name: string;
  /** Optional action button (e.g. "Publish" or "Enable"). */
  action?: React.ReactNode;
  className?: string;
}

export function UnpublishedEntity({ name, action, className }: UnpublishedEntityProps) {
  return (
    <div
      data-slot="unpublished-entity"
      className={cn(
        "flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-border bg-muted/50 px-6 py-10 text-center",
        className
      )}
    >
      <div className="flex size-12 items-center justify-center rounded-full bg-muted">
        <EyeOffIcon className="size-6 text-muted-foreground" />
      </div>
      <div className="flex flex-col gap-1">
        <p className="text-sm font-semibold text-foreground">
          <Trans>{name} is unpublished</Trans>
        </p>
        <p className="text-sm text-muted-foreground">
          <Trans>This page is not visible to the public.</Trans>
        </p>
      </div>
      {action && <div>{action}</div>}
    </div>
  );
}

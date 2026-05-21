import { Trans } from "@lingui/react/macro";

import { cn } from "../utils";
import { Badge } from "./Badge";

/**
 * A card-bordered section for an onboarding wizard step.
 * Ported from cal.com `packages/ui/components/card/StepCard.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface StepCardProps {
  /** Step number displayed in the badge. */
  step?: number;
  title: React.ReactNode;
  description?: React.ReactNode;
  children?: React.ReactNode;
  className?: string;
}

export function StepCard({ step, title, description, children, className }: StepCardProps) {
  return (
    <div data-slot="step-card" className={cn("rounded-xl border border-border bg-card p-6 shadow-xs", className)}>
      <div className="flex flex-col gap-4">
        <div className="flex items-start justify-between gap-4">
          <div className="flex flex-col gap-1">
            <h3 className="text-base font-semibold text-card-foreground">{title}</h3>
            {description && <p className="text-sm text-muted-foreground">{description}</p>}
          </div>
          {step !== undefined && (
            <Badge variant="secondary" className="shrink-0">
              <Trans>Step {step}</Trans>
            </Badge>
          )}
        </div>
        {children && <div className="flex flex-col gap-4">{children}</div>}
      </div>
    </div>
  );
}

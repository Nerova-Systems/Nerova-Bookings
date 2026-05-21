import { ChevronDownIcon } from "lucide-react";

import { cn } from "../utils";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "./Collapsible";

/**
 * Collapsible panel card for settings pages.
 * Ported from cal.com `packages/ui/components/card/PanelCard.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface PanelCardProps {
  title: React.ReactNode;
  description?: React.ReactNode;
  /** Badge / action in the header trailing area. */
  badge?: React.ReactNode;
  children: React.ReactNode;
  defaultOpen?: boolean;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  className?: string;
}

export function PanelCard({
  title,
  description,
  badge,
  children,
  defaultOpen = true,
  open,
  onOpenChange,
  className
}: PanelCardProps) {
  return (
    <Collapsible defaultOpen={defaultOpen} open={open} onOpenChange={onOpenChange}>
      <div data-slot="panel-card" className={cn("rounded-xl border border-border bg-card shadow-xs", className)}>
        <div className="flex items-center justify-between gap-4 px-6 py-4">
          <div className="flex flex-col gap-0.5">
            <h3 className="text-sm font-semibold text-card-foreground">{title}</h3>
            {description && <p className="text-sm text-muted-foreground">{description}</p>}
          </div>
          <div className="flex shrink-0 items-center gap-2">
            {badge}
            <CollapsibleTrigger className="inline-flex size-[var(--control-height-sm)] items-center justify-center rounded-[min(var(--radius-md),0.625rem)] text-muted-foreground outline-ring transition-colors hover:bg-muted hover:text-foreground focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2">
              <ChevronDownIcon className="size-4 transition-transform [[data-open]>&]:rotate-180" />
            </CollapsibleTrigger>
          </div>
        </div>
        <CollapsibleContent>
          <div className="border-t border-border px-6 py-5">{children}</div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}

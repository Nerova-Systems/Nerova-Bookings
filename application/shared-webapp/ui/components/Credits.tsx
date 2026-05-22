import type { ReactNode } from "react";

import { cn } from "../utils";

/**
 * EE billing credits display.
 * Ported from cal.com `packages/ui/components/credits/Credits.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface CreditsProps {
  /** Number of credits remaining. */
  credits: number;
  /** Optional label override. Defaults to "credits". */
  label?: ReactNode;
  className?: string;
}

export function Credits({ credits, label = "credits", className }: CreditsProps) {
  return (
    <span
      data-slot="credits"
      className={cn("inline-flex items-center gap-1 text-sm font-medium text-foreground tabular-nums", className)}
    >
      <span className="font-bold">{new Intl.NumberFormat().format(credits)}</span>
      <span className="text-muted-foreground">{label}</span>
    </span>
  );
}

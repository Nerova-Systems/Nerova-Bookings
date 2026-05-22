import { cn } from "../utils";

/**
 * Page-section heading with optional subtitle and trailing action slot.
 * Ported from cal.com `packages/ui/components/layout/ShellSubHeading.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface ShellSubHeadingProps {
  title: React.ReactNode;
  subtitle?: React.ReactNode;
  actions?: React.ReactNode;
  className?: string;
}

export function ShellSubHeading({ title, subtitle, actions, className }: ShellSubHeadingProps) {
  return (
    <div data-slot="shell-sub-heading" className={cn("flex items-center justify-between gap-4", className)}>
      <div className="flex flex-col gap-0.5">
        <h3 className="text-sm leading-none font-semibold text-foreground">{title}</h3>
        {subtitle && <p className="text-sm text-muted-foreground">{subtitle}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}

import { cn } from "../utils";

/**
 * Settings page section wrapper with title, description, and content area.
 * Ported from cal.com `packages/ui/components/section/section.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface SectionProps {
  title?: React.ReactNode;
  description?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}

export function Section({ title, description, children, className }: SectionProps) {
  return (
    <section data-slot="section" className={cn("rounded-xl border border-border bg-card", className)}>
      {(title || description) && (
        <div className="border-b border-border px-6 py-4">
          {title && <h2 className="text-base font-semibold text-card-foreground">{title}</h2>}
          {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
        </div>
      )}
      <div className="px-6 py-5">{children}</div>
    </section>
  );
}

function SectionHeader({ className, ...props }: React.ComponentProps<"div">) {
  return <div data-slot="section-header" className={cn("border-b border-border px-6 py-4", className)} {...props} />;
}

function SectionContent({ className, ...props }: React.ComponentProps<"div">) {
  return <div data-slot="section-content" className={cn("px-6 py-5", className)} {...props} />;
}

export { SectionHeader, SectionContent };

import type { LucideIcon, LucideProps } from "lucide-react";

import * as LucideIcons from "lucide-react";

import { cn } from "../utils";

/**
 * A Lucide icon wrapper that re-exports all Lucide icons with consistent sizing.
 * Ported from cal.com `packages/ui/components/icon/Icon.tsx` (cf2a55c).
 *
 * Deviation: cal.com maintains a curated icon allow-list. Nerova imports directly from `lucide-react`
 * without a constrained list, which is consistent with how Nerova components already use Lucide.
 * The `name` prop accepts any `keyof typeof LucideIcons` that is a `LucideIcon`.
 */
export type IconName = {
  [K in keyof typeof LucideIcons]: (typeof LucideIcons)[K] extends LucideIcon ? K : never;
}[keyof typeof LucideIcons];

interface IconProps extends LucideProps {
  /** The icon name from the lucide-react library (PascalCase). */
  name: IconName;
}

export function Icon({ name, className, ...props }: IconProps) {
  const Component = LucideIcons[name] as LucideIcon;

  if (!Component) {
    return null;
  }

  return <Component data-slot="icon" className={cn("size-4", className)} {...props} />;
}

/** Re-export Lucide for convenience. */
export { LucideIcons };

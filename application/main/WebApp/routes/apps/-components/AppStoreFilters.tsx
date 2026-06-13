import { Trans } from "@lingui/react/macro";
import { cn } from "@repo/ui/utils";
import { BlocksIcon, CalendarDaysIcon, CreditCardIcon, type LucideIcon, VideoIcon } from "lucide-react";

import { AppCategory } from "@/shared/lib/api/client";

import { getAppCategoryLabel } from "./appsTypes";

const CATEGORY_ICON: Readonly<Record<AppCategory, LucideIcon>> = {
  [AppCategory.Calendar]: CalendarDaysIcon,
  [AppCategory.Conferencing]: VideoIcon,
  [AppCategory.Payment]: CreditCardIcon,
  [AppCategory.Other]: BlocksIcon
};

/** Per-category gradient backdrop for the featured-category slider tiles (decision 3A). */
const CATEGORY_GRADIENT: Readonly<Record<AppCategory, string>> = {
  [AppCategory.Calendar]: "from-sky-500/15 to-blue-500/5 text-sky-600 dark:text-sky-400",
  [AppCategory.Conferencing]: "from-violet-500/15 to-fuchsia-500/5 text-violet-600 dark:text-violet-400",
  [AppCategory.Payment]: "from-primary/15 to-primary/5 text-primary",
  [AppCategory.Other]: "from-amber-500/15 to-orange-500/5 text-amber-600 dark:text-amber-400"
};

export function CategoryCard({
  category,
  count,
  onClick
}: Readonly<{ category: AppCategory; count: number; onClick: () => void }>) {
  const Icon = CATEGORY_ICON[category];
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "group flex h-36 w-full flex-col justify-between rounded-xl border border-border bg-gradient-to-br p-5 text-left outline-ring transition-all hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
        CATEGORY_GRADIENT[category]
      )}
    >
      <div className="flex size-11 items-center justify-center rounded-lg bg-background/70 shadow-sm backdrop-blur-sm">
        <Icon className="size-5" />
      </div>
      <div className="min-w-0">
        <p className="font-semibold text-foreground">{getAppCategoryLabel(category)}</p>
        <p className="text-sm text-muted-foreground">
          {count === 1 ? <Trans>{count} app</Trans> : <Trans>{count} apps</Trans>}
        </p>
      </div>
    </button>
  );
}

export function CategoryPill({
  label,
  isActive,
  onClick
}: Readonly<{ label: string; isActive: boolean; onClick: () => void }>) {
  return (
    <li>
      <button
        type="button"
        role="tab"
        aria-selected={isActive}
        onClick={onClick}
        className={cn(
          "min-w-max rounded-md px-4 py-2 text-sm font-medium whitespace-nowrap outline-ring transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
          isActive ? "bg-primary text-primary-foreground" : "bg-muted text-foreground hover:bg-muted/70"
        )}
      >
        {label}
      </button>
    </li>
  );
}

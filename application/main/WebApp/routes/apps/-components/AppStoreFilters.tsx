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
      className="group flex items-center gap-4 rounded-lg border border-border bg-card p-5 text-left outline-ring transition-colors hover:border-primary/40 hover:bg-accent/40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2"
    >
      <div className="flex size-12 items-center justify-center rounded-lg bg-primary/10 text-primary transition-colors group-hover:bg-primary/15">
        <Icon className="size-6" />
      </div>
      <div className="min-w-0">
        <p className="font-medium text-foreground">{getAppCategoryLabel(category)}</p>
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

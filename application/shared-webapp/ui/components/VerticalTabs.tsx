import type React from "react";

import { ChevronRightIcon } from "lucide-react";

import { cn } from "../utils";
import { Button } from "./Button";

export type VerticalTabItem = Readonly<{
  value: string;
  label: React.ReactNode;
  description?: React.ReactNode;
  icon?: React.ReactNode;
  disabled?: boolean;
}>;

export function VerticalTabs({
  tabs,
  value,
  onValueChange,
  className,
  itemClassName,
  "data-testid": dataTestId
}: Readonly<{
  tabs: VerticalTabItem[];
  value: string;
  onValueChange: (value: string) => void;
  className?: string;
  itemClassName?: string;
  "data-testid"?: string;
}>) {
  return (
    <nav
      aria-label="Tabs"
      aria-orientation="vertical"
      className={cn("no-scrollbar flex min-w-0 flex-col gap-1 overflow-y-auto", className)}
      data-testid={dataTestId}
      role="tablist"
    >
      {tabs.map((tab) => {
        const isActive = tab.value === value;
        return (
          <Button
            key={tab.value}
            type="button"
            variant="ghost"
            disabled={tab.disabled}
            aria-current={isActive ? "page" : undefined}
            aria-selected={isActive}
            data-testid={`event-type-tab-${tab.value}`}
            role="tab"
            className={cn(
              "h-auto w-full justify-start gap-3 rounded-md px-2 py-2 text-left max-sm:w-full",
              "text-muted-foreground hover:bg-muted hover:text-foreground",
              "aria-[current=page]:bg-muted aria-[current=page]:text-foreground",
              itemClassName
            )}
            onClick={() => onValueChange(tab.value)}
          >
            {tab.icon && <span className="mt-0.5 flex size-4 shrink-0 items-center justify-center">{tab.icon}</span>}
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm leading-none font-medium">{tab.label}</span>
              {tab.description && (
                <span className="mt-1 block truncate text-xs leading-none font-normal text-muted-foreground">
                  {tab.description}
                </span>
              )}
            </span>
            {isActive && <ChevronRightIcon className="ml-auto size-4 shrink-0 text-muted-foreground" />}
          </Button>
        );
      })}
    </nav>
  );
}

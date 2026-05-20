import { cn } from "../utils";
import { Button } from "./Button";
import { type VerticalTabItem } from "./VerticalTabs";

export function HorizontalTabs({
  tabs,
  value,
  onValueChange,
  className,
  "data-testid": dataTestId
}: Readonly<{
  tabs: VerticalTabItem[];
  value: string;
  onValueChange: (value: string) => void;
  className?: string;
  "data-testid"?: string;
}>) {
  return (
    <nav
      aria-label="Tabs"
      aria-orientation="horizontal"
      className={cn("no-scrollbar flex min-w-0 gap-1 overflow-x-auto border-b pb-2", className)}
      data-testid={dataTestId}
      role="tablist"
    >
      {tabs.map((tab) => (
        <Button
          key={tab.value}
          type="button"
          variant="ghost"
          size="sm"
          disabled={tab.disabled}
          aria-current={tab.value === value ? "page" : undefined}
          aria-selected={tab.value === value}
          data-testid={`event-type-tab-${tab.value}`}
          role="tab"
          className="shrink-0 text-muted-foreground aria-[current=page]:bg-muted aria-[current=page]:text-foreground"
          onClick={() => onValueChange(tab.value)}
        >
          {tab.icon}
          {tab.label}
        </Button>
      ))}
    </nav>
  );
}

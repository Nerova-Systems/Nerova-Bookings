import { useLingui } from "@lingui/react/macro";
import { ChevronDownIcon } from "lucide-react";

import { cn } from "../utils";
import { Button } from "./Button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "./DropdownMenu";

/**
 * A button split into a primary action and a dropdown chevron for secondary actions.
 * Ported from cal.com `packages/ui/components/button/SplitButton.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface SplitButtonOption {
  label: React.ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  variant?: "default" | "destructive";
}

interface SplitButtonProps {
  /** Primary button label. */
  children: React.ReactNode;
  /** Primary button click handler. */
  onClick?: () => void;
  /** Secondary options shown in the dropdown. */
  options?: SplitButtonOption[];
  variant?: "default" | "outline" | "secondary";
  size?: "default" | "sm" | "xs";
  disabled?: boolean;
  isPending?: boolean;
  className?: string;
}

export function SplitButton({
  children,
  onClick,
  options = [],
  variant = "default",
  size = "default",
  disabled,
  isPending,
  className
}: SplitButtonProps) {
  const { t } = useLingui();

  return (
    <div data-slot="split-button" className={cn("inline-flex items-stretch rounded-md", className)}>
      <Button
        variant={variant}
        size={size}
        onClick={onClick}
        disabled={disabled}
        isPending={isPending}
        className="rounded-r-none focus-visible:z-10"
      >
        {children}
      </Button>
      {options.length > 0 && (
        <DropdownMenu>
          <DropdownMenuTrigger
            data-slot="split-button-trigger"
            disabled={disabled}
            aria-label={t`More options`}
            className={cn(
              "inline-flex cursor-pointer items-center justify-center rounded-l-none rounded-r-md border-l border-l-primary/20 px-2 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 disabled:pointer-events-none disabled:opacity-50",
              variant === "default" && "bg-primary text-primary-foreground outline-primary hover:bg-primary/80",
              variant === "outline" && "border border-border bg-white hover:bg-muted",
              variant === "secondary" && "bg-white hover:bg-muted"
            )}
          >
            <ChevronDownIcon className="size-4" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {options.map((opt, i) => (
              <DropdownMenuItem
                key={i}
                variant={opt.variant === "destructive" ? "destructive" : "default"}
                disabled={opt.disabled}
                onClick={opt.onClick}
              >
                {opt.label}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  );
}

import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";

import { cn } from "../utils";
import { Button } from "./Button";

/**
 * Directional navigation button with a chevron icon.
 * Ported from cal.com `packages/ui/components/arrow-button/ArrowButton.tsx` (cf2a55c).
 *
 * Prop deviation: `arrowDirection` replaces cal.com's `direction`; matches Nerova naming
 * convention for directional props to avoid shadowing HTML `dir`.
 */
interface ArrowButtonProps {
  arrowDirection?: "left" | "right";
  variant?: "default" | "outline" | "secondary" | "ghost" | "destructive" | "link";
  size?: "default" | "sm" | "xs" | "icon" | "icon-sm" | "icon-xs";
  disabled?: boolean;
  onClick?: React.MouseEventHandler<HTMLButtonElement>;
  className?: string;
  "aria-label"?: string;
}

export function ArrowButton({
  arrowDirection = "right",
  variant = "ghost",
  size = "icon",
  className,
  ...props
}: ArrowButtonProps) {
  const Icon = arrowDirection === "left" ? ChevronLeftIcon : ChevronRightIcon;

  return (
    <Button data-slot="arrow-button" variant={variant} size={size} className={cn(className)} {...props}>
      <Icon />
    </Button>
  );
}

import { XIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { createPortal } from "react-dom";

import { cn } from "../utils";
import { Button } from "./Button";

/**
 * Dismissible top banner with variant-based styling.
 * Ported from cal.com `packages/ui/components/top-banner/TopBanner.tsx` (cf2a55c).
 *
 * Deviation vs cal.com:
 * - Renders into `#banner-root` via portal if it exists (matching Nerova's BannerPortal).
 *   Falls back to inline rendering if no portal target is mounted.
 * - cal.com renders inline; Nerova's BannerPortal is the preferred injection point.
 */
type TopBannerVariant = "default" | "warning" | "error" | "info";

interface TopBannerProps {
  text: React.ReactNode;
  variant?: TopBannerVariant;
  actions?: React.ReactNode;
  dismissible?: boolean;
  onDismiss?: () => void;
  className?: string;
}

const variantClasses: Record<TopBannerVariant, string> = {
  default: "bg-primary text-primary-foreground",
  warning: "bg-warning text-warning-foreground",
  error: "bg-destructive text-destructive-foreground",
  info: "bg-info text-info-foreground"
};

function TopBannerContent({ text, variant = "default", actions, dismissible, onDismiss, className }: TopBannerProps) {
  return (
    <div
      data-slot="top-banner"
      data-variant={variant}
      className={cn(
        "flex w-full items-center justify-center gap-3 px-4 py-2.5 text-sm font-medium",
        variantClasses[variant],
        className
      )}
      role="status"
    >
      <span className="flex-1 text-center">{text}</span>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      {dismissible && (
        <Button
          variant="ghost"
          size="icon-xs"
          className="shrink-0 opacity-80 hover:opacity-100"
          onClick={onDismiss}
          aria-label="Dismiss banner"
        >
          <XIcon />
        </Button>
      )}
    </div>
  );
}

export function TopBanner(props: TopBannerProps) {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) return null;

  const portalRoot = document.getElementById("banner-root");
  if (portalRoot) {
    return createPortal(<TopBannerContent {...props} />, portalRoot);
  }
  return <TopBannerContent {...props} />;
}

import { Trans } from "@lingui/react/macro";
import { Button, buttonVariants } from "@repo/ui/components/Button";
import { cn } from "@repo/ui/utils";
import { Link as RouterLink } from "@tanstack/react-router";
import { ArrowRightIcon } from "lucide-react";

type ChannelStatus = "connected" | "available" | "loading" | "coming-soon";

interface ChannelCardProps {
  icon: React.ReactNode;
  name: string;
  description: string;
  status: ChannelStatus;
  /** Optional accent gradient applied to the card header band (e.g. WhatsApp green). */
  accentClassName?: string;
  to?: "/channels/whatsapp";
}

/**
 * Channel overview card. Shares the rounded-card language of the App Store cards but is intentionally
 * richer and more status-forward — a colored accent band, a live status pill, and a full-width primary
 * action — because channels carry live connection state (WhatsApp) versus the lighter app catalog.
 */
export function ChannelCard({ icon, name, description, status, accentClassName, to }: Readonly<ChannelCardProps>) {
  const isInteractive = (status === "connected" || status === "available") && to !== undefined;

  return (
    <div className="flex flex-col overflow-hidden rounded-xl border border-border bg-card shadow-sm">
      <div className={cn("flex items-center justify-between gap-3 border-b border-border/60 p-5", accentClassName)}>
        <div className="flex size-12 items-center justify-center rounded-lg border bg-background/80 shadow-sm backdrop-blur-sm">
          {icon}
        </div>
        <StatusPill status={status} />
      </div>

      <div className="flex grow flex-col p-5">
        <h3 className="font-semibold text-foreground">{name}</h3>
        <p className="mt-2 line-clamp-3 grow text-sm text-muted-foreground">{description}</p>

        <div className="mt-5">
          {isInteractive ? (
            <RouterLink
              to={to}
              className={buttonVariants({
                variant: status === "connected" ? "outline" : "default",
                size: "sm",
                className: "w-full justify-center"
              })}
            >
              {status === "connected" ? <Trans>Manage</Trans> : <Trans>Connect</Trans>}
              <ArrowRightIcon className="size-4" />
            </RouterLink>
          ) : (
            <Button variant="outline" size="sm" disabled className="w-full justify-center">
              {status === "loading" ? <Trans>Checking…</Trans> : <Trans>Coming soon</Trans>}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

function StatusPill({ status }: Readonly<{ status: ChannelStatus }>) {
  if (status === "connected") {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full bg-primary/10 px-2.5 py-1 text-xs font-medium text-primary dark:bg-primary/20 ">
        <span className="size-2 animate-pulse rounded-full bg-primary" />
        <Trans>Connected</Trans>
      </span>
    );
  }
  if (status === "loading") {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-1 text-xs font-medium text-muted-foreground">
        <span className="size-2 animate-pulse rounded-full bg-muted-foreground/50" />
        <Trans>Checking…</Trans>
      </span>
    );
  }
  if (status === "available") {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-1 text-xs font-medium text-muted-foreground">
        <span className="size-2 rounded-full bg-muted-foreground/50" />
        <Trans>Not connected</Trans>
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-1 text-xs font-medium text-muted-foreground">
      <Trans>Coming soon</Trans>
    </span>
  );
}

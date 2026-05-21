import { Trans, useLingui } from "@lingui/react/macro";
import { ExternalLinkIcon, SettingsIcon } from "lucide-react";

import { cn } from "../utils";
import { Avatar, AvatarFallback, AvatarImage } from "./Avatar";
import { Badge } from "./Badge";
import { Button } from "./Button";

/**
 * App connector card showing an app's icon, name, description, install status, and CTA.
 * Ported from cal.com `packages/ui/components/apps/AppListCard.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface AppListCardProps {
  /** App logo URL. */
  logo?: string;
  /** App name displayed as title. */
  name: string;
  /** Short description / tagline. */
  description?: string;
  /** Whether the app is currently installed/connected. */
  isInstalled?: boolean;
  /** Whether the app is a paid/premium app. */
  isPaid?: boolean;
  /** External link to the app's marketplace page. */
  href?: string;
  /** Called when the user clicks the settings/configure button. */
  onSettings?: () => void;
  /** Called when the user clicks the install/connect CTA. */
  onInstall?: () => void;
  /** Called when the user clicks the disconnect/uninstall CTA. */
  onUninstall?: () => void;
  /** Override content for the CTA area. */
  actions?: React.ReactNode;
  /** Slot for additional badges. */
  badge?: React.ReactNode;
  className?: string;
}

export function AppListCard({
  logo,
  name,
  description,
  isInstalled,
  isPaid,
  href,
  onSettings,
  onInstall,
  onUninstall,
  actions,
  badge,
  className
}: AppListCardProps) {
  const { t } = useLingui();
  const initials = name.slice(0, 2).toUpperCase();

  return (
    <div
      data-slot="app-list-card"
      className={cn("flex items-center gap-4 rounded-xl border border-border bg-card px-6 py-4 shadow-xs", className)}
    >
      {/* App icon */}
      <Avatar size="lg">
        {logo && <AvatarImage src={logo} alt={name} />}
        <AvatarFallback>{initials}</AvatarFallback>
      </Avatar>

      {/* Info */}
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm font-semibold text-foreground">{name}</span>
          {isPaid && (
            <Badge variant="secondary">
              <Trans>Paid</Trans>
            </Badge>
          )}
          {isInstalled && (
            <Badge variant="default">
              <Trans>Installed</Trans>
            </Badge>
          )}
          {badge}
        </div>
        {description && <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">{description}</p>}
      </div>

      {/* Actions */}
      <div className="flex shrink-0 items-center gap-2">
        {actions ?? (
          <>
            {href && (
              <a
                href={href}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex size-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                aria-label={t`View ${name} in marketplace`}
              >
                <ExternalLinkIcon className="size-4" />
              </a>
            )}
            {isInstalled ? (
              <>
                {onSettings && (
                  <Button variant="outline" size="sm" onClick={onSettings} aria-label={t`Configure ${name}`}>
                    <SettingsIcon className="size-4" />
                    <Trans>Settings</Trans>
                  </Button>
                )}
                {onUninstall && (
                  <Button variant="outline" size="sm" onClick={onUninstall} aria-label={t`Disconnect ${name}`}>
                    <Trans>Disconnect</Trans>
                  </Button>
                )}
              </>
            ) : (
              onInstall && (
                <Button size="sm" onClick={onInstall} aria-label={t`Install ${name}`}>
                  <Trans>Install</Trans>
                </Button>
              )
            )}
          </>
        )}
      </div>
    </div>
  );
}

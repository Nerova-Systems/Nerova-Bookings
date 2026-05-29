import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { CheckCircle2Icon } from "lucide-react";

import type { App } from "./appsTypes";

import { getAppCategoryLabel } from "./appsTypes";

interface AppStoreCardProps {
  app: App;
  onClick: () => void;
}

export function AppStoreCard({ app, onClick }: Readonly<AppStoreCardProps>) {
  const isInstalled = app.slug === "whatsapp" ? app.isInstalledForTenant : app.isConnectedForUser;

  return (
    <div
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          onClick();
        }
      }}
      className="group relative flex cursor-pointer flex-col justify-between overflow-hidden rounded-xl border border-border bg-card p-5 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-primary/20 hover:shadow-md"
    >
      <div className="flex items-start gap-4">
        <AppIcon app={app} />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="font-semibold text-foreground transition-colors duration-200 group-hover:text-primary">
              {app.name}
            </span>
          </div>
          <p className="mt-1 line-clamp-2 min-h-[2rem] text-xs text-muted-foreground">{app.description}</p>
        </div>
      </div>

      <div className="mt-4 flex items-center justify-between border-t pt-3">
        <Badge variant="outline" className="text-[10px] font-semibold tracking-wider text-muted-foreground uppercase">
          {getAppCategoryLabel(app.category)}
        </Badge>
        <div>
          {isInstalled ? (
            <Badge className="flex items-center gap-1 border-emerald-500/20 bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
              <CheckCircle2Icon className="size-3" />
              <Trans>Installed</Trans>
            </Badge>
          ) : (
            <Badge variant="secondary" className="text-muted-foreground">
              <Trans>Explore</Trans>
            </Badge>
          )}
        </div>
      </div>
    </div>
  );
}

function AppIcon({ app }: Readonly<{ app: App }>) {
  if (app.logoUrl) {
    return (
      <img
        src={app.logoUrl}
        alt=""
        className="h-12 w-12 shrink-0 rounded-xl border bg-background object-contain p-2 shadow-sm transition-transform duration-300 group-hover:scale-105"
      />
    );
  }
  return (
    <div
      aria-hidden="true"
      className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl border bg-muted text-lg font-bold text-muted-foreground shadow-sm transition-transform duration-300 group-hover:scale-105"
    >
      {app.name.slice(0, 1).toUpperCase()}
    </div>
  );
}

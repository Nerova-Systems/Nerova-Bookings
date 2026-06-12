import { Trans } from "@lingui/react/macro";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { cn } from "@repo/ui/utils";
import {
  ArrowDownLeftIcon,
  ArrowUpRightIcon,
  CheckCheckIcon,
  MessageSquareIcon,
  TriangleAlertIcon,
  UsersIcon
} from "lucide-react";

import { api } from "@/shared/lib/api/client";

/**
 * Stats summary for the WhatsApp console. Consumes the dedicated GetWhatsAppStats query and surfaces
 * the headline messaging metrics (total volume, direction split, delivery health, reach) so the
 * channel page reads as a rich operations console rather than a simple connection toggle.
 */
export function WhatsAppStats() {
  const statsQuery = api.useQuery("get", "/api/main/whatsapp/messages/stats");
  const formatDate = useFormatDate();
  const stats = statsQuery.data;

  if (statsQuery.isLoading) {
    return (
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-3">
        {[...Array(6)].map((_, index) => (
          <Skeleton key={`stat-shimmer-${index}`} className="h-24 rounded-xl" />
        ))}
      </div>
    );
  }

  if (stats === undefined) return null;

  return (
    <section className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-4">
        <h2 className="text-base font-semibold text-foreground">
          <Trans>Activity</Trans>
        </h2>
        {stats.lastActivityAt !== null && (
          <span className="text-xs text-muted-foreground">
            <Trans>Last message {formatDate(stats.lastActivityAt, true)}</Trans>
          </span>
        )}
      </div>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-3">
        <StatTile
          icon={<MessageSquareIcon className="size-4" />}
          label={<Trans>Total messages</Trans>}
          value={stats.totalMessages}
          accent="text-foreground"
        />
        <StatTile
          icon={<ArrowDownLeftIcon className="size-4" />}
          label={<Trans>From clients</Trans>}
          value={stats.inboundCount}
          accent="text-primary"
        />
        <StatTile
          icon={<ArrowUpRightIcon className="size-4" />}
          label={<Trans>From you</Trans>}
          value={stats.outboundCount}
          accent="text-primary"
        />
        <StatTile
          icon={<CheckCheckIcon className="size-4" />}
          label={<Trans>Delivered</Trans>}
          value={stats.deliveredCount}
          accent="text-success"
        />
        <StatTile
          icon={<TriangleAlertIcon className="size-4" />}
          label={<Trans>Needs attention</Trans>}
          value={stats.failedCount}
          accent="text-destructive"
        />
        <StatTile
          icon={<UsersIcon className="size-4" />}
          label={<Trans>Unique contacts</Trans>}
          value={stats.uniqueContacts}
          accent="text-warning"
        />
      </div>
    </section>
  );
}

function StatTile({
  icon,
  label,
  value,
  accent
}: Readonly<{ icon: React.ReactNode; label: React.ReactNode; value: number; accent: string }>) {
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-border bg-card p-4">
      <div className={cn("flex items-center gap-1.5 text-xs font-medium text-muted-foreground", accent)}>
        {icon}
        <span className="text-muted-foreground">{label}</span>
      </div>
      <p className="text-2xl font-bold text-foreground tabular-nums">{value}</p>
    </div>
  );
}

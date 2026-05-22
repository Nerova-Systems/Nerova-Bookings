import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Skeleton } from "@repo/ui/components/Skeleton";

import type { Schemas } from "@/shared/lib/api/client";

type TopHosts = Schemas["TopHostsResponse"];

interface TopHostsListProps {
  data: TopHosts | undefined;
  isLoading: boolean;
}

export function TopHostsList({ data, isLoading }: Readonly<TopHostsListProps>) {
  const hosts = data?.hosts ?? [];
  const maxCount = hosts.reduce((max, host) => Math.max(max, host.totalCount), 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Top hosts</Trans>
        </CardTitle>
        <CardDescription>
          <Trans>Members handling the most bookings.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-3">
            {[0, 1, 2, 3, 4].map((index) => (
              <Skeleton key={index} className="h-6 w-full" />
            ))}
          </div>
        ) : hosts.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t`No bookings in the selected range.`}</p>
        ) : (
          <ul className="space-y-3">
            {hosts.map((host) => {
              const widthPercent = maxCount > 0 ? Math.max(4, (host.totalCount / maxCount) * 100) : 0;
              return (
                <li key={host.hostUserId} className="flex items-center gap-3">
                  <span className="min-w-0 flex-1 truncate font-mono text-xs text-muted-foreground">
                    {host.hostUserId}
                  </span>
                  <div className="relative h-2 w-32 overflow-hidden rounded-full bg-muted">
                    <div
                      className="absolute inset-y-0 left-0 rounded-full bg-(--chart-3)"
                      style={{ width: `${widthPercent}%`, backgroundColor: "var(--chart-3)" }}
                    />
                  </div>
                  <span className="w-12 text-right text-sm tabular-nums">{host.totalCount}</span>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

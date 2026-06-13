import { Trans } from "@lingui/react/macro";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { ArrowDownIcon, ArrowUpIcon, MinusIcon } from "lucide-react";
import { type ReactNode } from "react";

import type { Schemas } from "@/shared/lib/api/client";

type Kpis = Schemas["BookingKpisResponse"];

interface InsightsKpiTilesProps {
  kpis: Kpis | undefined;
  isLoading: boolean;
}

export function InsightsKpiTiles({ kpis, isLoading }: Readonly<InsightsKpiTilesProps>) {
  if (isLoading || !kpis) {
    return (
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[0, 1, 2, 3].map((index) => (
          <Card key={index}>
            <CardHeader>
              <Skeleton className="h-4 w-24" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-8 w-20" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  const cancellationRate = kpis.totalCount > 0 ? kpis.cancelledCount / kpis.totalCount : 0;
  const priorCancellationRate =
    kpis.priorPeriodTotalCount > 0 ? kpis.priorPeriodCancelledCount / kpis.priorPeriodTotalCount : 0;
  const acceptanceRate = kpis.totalCount > 0 ? kpis.acceptedCount / kpis.totalCount : 0;
  const priorAcceptanceRate =
    kpis.priorPeriodTotalCount > 0 ? kpis.priorPeriodAcceptedCount / kpis.priorPeriodTotalCount : 0;

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <KpiTile
        label={<Trans>Total bookings</Trans>}
        value={formatNumber(kpis.totalCount)}
        delta={percentageDelta(kpis.totalCount, kpis.priorPeriodTotalCount)}
      />
      <KpiTile
        label={<Trans>Accepted</Trans>}
        value={formatNumber(kpis.acceptedCount)}
        delta={percentagePointsDelta(acceptanceRate, priorAcceptanceRate)}
        deltaSuffix="pp"
      />
      <KpiTile
        label={<Trans>Cancellation rate</Trans>}
        value={formatPercent(cancellationRate)}
        delta={percentagePointsDelta(cancellationRate, priorCancellationRate)}
        deltaSuffix="pp"
        invertDelta={true}
      />
      <KpiTile label={<Trans>Completed</Trans>} value={formatNumber(kpis.completedCount)} />
    </div>
  );
}

interface KpiTileProps {
  label: ReactNode;
  value: string;
  delta?: number;
  deltaSuffix?: string;
  invertDelta?: boolean;
}

function KpiTile({ label, value, delta, deltaSuffix, invertDelta }: Readonly<KpiTileProps>) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="flex items-baseline gap-2">
          <span className="text-2xl font-semibold tabular-nums">{value}</span>
          {delta !== undefined && <DeltaBadge value={delta} suffix={deltaSuffix} invert={invertDelta} />}
        </div>
      </CardContent>
    </Card>
  );
}

function DeltaBadge({ value, suffix, invert }: Readonly<{ value: number; suffix?: string; invert?: boolean }>) {
  if (!Number.isFinite(value)) {
    return null;
  }

  const isPositive = value > 0.0005;
  const isNegative = value < -0.0005;
  const isFlat = !isPositive && !isNegative;
  const isGood = invert ? isNegative : isPositive;
  const isBad = invert ? isPositive : isNegative;

  const Icon = isFlat ? MinusIcon : isPositive ? ArrowUpIcon : ArrowDownIcon;
  const tone = isFlat ? "text-muted-foreground" : isGood ? "text-primary" : isBad ? "text-rose-600" : "";

  const formatted = suffix === "pp" ? `${(value * 100).toFixed(1)}pp` : `${(value * 100).toFixed(1)}%`;

  return (
    <span className={`inline-flex items-center gap-1 text-xs tabular-nums ${tone}`}>
      <Icon className="size-3" />
      {formatted}
    </span>
  );
}

function percentageDelta(current: number, prior: number): number {
  if (prior === 0) {
    return current === 0 ? 0 : Number.POSITIVE_INFINITY;
  }
  return (current - prior) / prior;
}

function percentagePointsDelta(currentRate: number, priorRate: number): number {
  return currentRate - priorRate;
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat().format(value);
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`;
}

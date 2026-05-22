import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import {
  Cell,
  type ChartConfig,
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  Pie,
  PieChart
} from "@repo/ui/components/Chart";
import { Skeleton } from "@repo/ui/components/Skeleton";

import type { Schemas } from "@/shared/lib/api/client";

type Kpis = Schemas["BookingKpisResponse"];

interface BookingStatusBreakdownProps {
  kpis: Kpis | undefined;
  isLoading: boolean;
}

export function BookingStatusBreakdown({ kpis, isLoading }: Readonly<BookingStatusBreakdownProps>) {
  const chartConfig = {
    accepted: { label: t`Accepted`, color: "var(--chart-1)" },
    pending: { label: t`Pending`, color: "var(--chart-4)" },
    cancelled: { label: t`Cancelled`, color: "var(--chart-5)" },
    completed: { label: t`Completed`, color: "var(--chart-2)" }
  } satisfies ChartConfig;

  const chartData = kpis
    ? [
        { name: "accepted", value: kpis.acceptedCount, fill: "var(--color-accepted)" },
        { name: "pending", value: kpis.pendingCount, fill: "var(--color-pending)" },
        { name: "cancelled", value: kpis.cancelledCount, fill: "var(--color-cancelled)" },
        { name: "completed", value: kpis.completedCount, fill: "var(--color-completed)" }
      ].filter((entry) => entry.value > 0)
    : [];

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Status breakdown</Trans>
        </CardTitle>
        <CardDescription>
          <Trans>Distribution of bookings by status.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="aspect-square h-[15.625rem] w-full" />
        ) : chartData.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t`No bookings in the selected range.`}</p>
        ) : (
          <ChartContainer config={chartConfig} className="mx-auto aspect-square h-[15.625rem]">
            <PieChart>
              <ChartTooltip content={<ChartTooltipContent nameKey="name" hideLabel={true} />} />
              <Pie data={chartData} dataKey="value" nameKey="name" innerRadius={60} strokeWidth={2}>
                {chartData.map((entry) => (
                  <Cell key={entry.name} fill={entry.fill} />
                ))}
              </Pie>
              <ChartLegend content={<ChartLegendContent nameKey="name" />} />
            </PieChart>
          </ChartContainer>
        )}
      </CardContent>
    </Card>
  );
}

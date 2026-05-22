import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import {
  Bar,
  BarChart,
  CartesianGrid,
  type ChartConfig,
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  XAxis,
  YAxis
} from "@repo/ui/components/Chart";
import { Skeleton } from "@repo/ui/components/Skeleton";

import type { Schemas } from "@/shared/lib/api/client";

type TopEventTypes = Schemas["TopEventTypesResponse"];

interface TopEventTypesChartProps {
  data: TopEventTypes | undefined;
  isLoading: boolean;
}

export function TopEventTypesChart({ data, isLoading }: Readonly<TopEventTypesChartProps>) {
  const chartConfig = {
    totalCount: { label: t`Bookings`, color: "var(--chart-2)" }
  } satisfies ChartConfig;

  const chartData = (data?.eventTypes ?? []).map((entry) => ({
    title: entry.title,
    totalCount: entry.totalCount,
    cancellationRate: entry.cancellationRate
  }));

  return (
    <Card className="pt-0">
      <CardHeader className="border-b py-5">
        <CardTitle>
          <Trans>Top event types</Trans>
        </CardTitle>
        <CardDescription>
          <Trans>Most booked event types in the selected range.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent className="px-2 pt-4 sm:px-6 sm:pt-6">
        {isLoading ? (
          <Skeleton className="aspect-auto h-[15.625rem] w-full" />
        ) : chartData.length === 0 ? (
          <EmptyState message={t`No bookings in the selected range.`} />
        ) : (
          <ChartContainer config={chartConfig} className="aspect-auto h-[15.625rem] w-full">
            <BarChart data={chartData} layout="vertical" margin={{ left: 16 }}>
              <CartesianGrid horizontal={false} />
              <XAxis type="number" tickLine={false} axisLine={false} allowDecimals={false} />
              <YAxis
                type="category"
                dataKey="title"
                tickLine={false}
                axisLine={false}
                width={140}
                tick={{ fontSize: 12 }}
              />
              <ChartTooltip cursor={false} content={<ChartTooltipContent indicator="dot" />} />
              <Bar dataKey="totalCount" fill="var(--color-totalCount)" radius={[0, 4, 4, 0]} />
            </BarChart>
          </ChartContainer>
        )}
      </CardContent>
    </Card>
  );
}

function EmptyState({ message }: Readonly<{ message: string }>) {
  return (
    <div className="flex h-[15.625rem] w-full items-center justify-center text-sm text-muted-foreground">{message}</div>
  );
}

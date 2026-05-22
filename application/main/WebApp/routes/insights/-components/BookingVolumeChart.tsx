import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import {
  Area,
  AreaChart,
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

type Bookings = Schemas["BookingsOverTimeResponse"];

const shortDateFormatter = new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric" });

interface BookingVolumeChartProps {
  data: Bookings | undefined;
  isLoading: boolean;
}

export function BookingVolumeChart({ data, isLoading }: Readonly<BookingVolumeChartProps>) {
  const chartConfig = {
    count: { label: t`Bookings`, color: "var(--chart-1)" }
  } satisfies ChartConfig;

  const chartData = (data?.dataPoints ?? []).map((point) => ({ date: point.date, count: point.count }));

  return (
    <Card className="pt-0">
      <CardHeader className="border-b py-5">
        <CardTitle>
          <Trans>Booking volume</Trans>
        </CardTitle>
        <CardDescription>
          <Trans>Bookings created per day in the selected range.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent className="px-2 pt-4 sm:px-6 sm:pt-6">
        {isLoading ? (
          <Skeleton className="aspect-auto h-[15.625rem] w-full" />
        ) : chartData.length === 0 ? (
          <EmptyState message={t`No bookings in the selected range.`} />
        ) : (
          <ChartContainer config={chartConfig} className="aspect-auto h-[15.625rem] w-full">
            <AreaChart data={chartData}>
              <defs>
                <linearGradient id="insights-volume-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="var(--color-count)" stopOpacity={0.8} />
                  <stop offset="95%" stopColor="var(--color-count)" stopOpacity={0.1} />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} />
              <XAxis
                dataKey="date"
                tickLine={false}
                axisLine={false}
                tickMargin={8}
                minTickGap={32}
                tickFormatter={(value: string) => shortDateFormatter.format(new Date(value))}
              />
              <YAxis tickLine={false} axisLine={false} tickMargin={8} allowDecimals={false} />
              <ChartTooltip
                cursor={false}
                content={
                  <ChartTooltipContent
                    labelFormatter={(value) => shortDateFormatter.format(new Date(value as string))}
                    indicator="dot"
                  />
                }
              />
              <Area
                dataKey="count"
                type="natural"
                fill="url(#insights-volume-fill)"
                stroke="var(--color-count)"
                strokeWidth={2}
              />
            </AreaChart>
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

import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Link } from "@repo/ui/components/Link";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { CalendarCheckIcon, SparklesIcon } from "lucide-react";

import type { BookingListItem } from "../../-bookings/bookingTypes";

import { formatBookingTime } from "./dashboardHelpers";

export function EmptyToday() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 rounded-xl border bg-card p-10 text-center">
      <div className="flex size-16 items-center justify-center rounded-full bg-muted">
        <SparklesIcon className="size-8 text-muted-foreground" />
      </div>
      <div className="flex flex-col gap-2">
        <h2>
          <Trans>Nothing needs you right now</Trans>
        </h2>
        <p className="max-w-md text-sm text-muted-foreground">
          <Trans>Nerova will bring important client messages and today's bookings here.</Trans>
        </p>
      </div>
    </div>
  );
}
export function TodaySummary({
  needsYouCount,
  handledCount,
  isLoading
}: Readonly<{ needsYouCount: number; handledCount: number; isLoading: boolean }>) {
  if (isLoading) {
    return (
      <div className="grid gap-3 sm:grid-cols-2">
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-24 rounded-xl" />
      </div>
    );
  }

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      <Link href="/channels/whatsapp" variant="button-secondary" underline={false} className="h-auto justify-start p-0">
        <Card className="w-full border-warning/30 bg-warning/10 py-4 shadow-none">
          <CardContent className="flex items-center justify-between gap-3 px-4">
            <div className="flex flex-col gap-1 text-left">
              <span className="text-sm font-medium">
                <Trans>Needs you</Trans>
              </span>
              <span className="text-sm text-muted-foreground">
                <Trans>Client messages waiting for your call</Trans>
              </span>
            </div>
            <Badge variant={needsYouCount > 0 ? "warning" : "secondary"}>
              <Trans>{needsYouCount} open</Trans>
            </Badge>
          </CardContent>
        </Card>
      </Link>
      <Card className="py-4 shadow-none">
        <CardContent className="flex items-center justify-between gap-3 px-4">
          <div className="flex flex-col gap-1">
            <span className="text-sm font-medium">
              <Trans>Handled by Nerova</Trans>
            </span>
            <span className="text-sm text-muted-foreground">
              <Trans>Finished in the last 24 hours</Trans>
            </span>
          </div>
          <Badge variant="default">
            <Trans>{handledCount} done</Trans>
          </Badge>
        </CardContent>
      </Card>
    </div>
  );
}

export function TodaysBookings({ bookings, isLoading }: Readonly<{ bookings: BookingListItem[]; isLoading: boolean }>) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <CalendarCheckIcon className="size-4 text-primary" />
          <CardTitle>
            <Trans>Today's bookings</Trans>
          </CardTitle>
        </div>
        <CardDescription>
          <Trans>Time, client, and service at a glance.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {isLoading ? (
          <>
            <Skeleton className="h-14 rounded-lg" />
            <Skeleton className="h-14 rounded-lg" />
            <Skeleton className="h-14 rounded-lg" />
          </>
        ) : bookings.length === 0 ? (
          <div className="rounded-lg border bg-muted/30 p-4 text-sm text-muted-foreground">
            <Trans>No bookings today.</Trans>
          </div>
        ) : (
          bookings.map((booking) => <TodayBookingRow key={booking.id} booking={booking} />)
        )}
      </CardContent>
    </Card>
  );
}

export function TodayBookingRow({ booking }: Readonly<{ booking: BookingListItem }>) {
  return (
    <div className="grid gap-3 rounded-lg border p-3 sm:grid-cols-[7rem_1fr_1fr] sm:items-center">
      <time className="text-sm font-medium tabular-nums" dateTime={booking.startTime}>
        {formatBookingTime(booking)}
      </time>
      <div className="min-w-0">
        <div className="truncate text-sm font-medium">{booking.bookerName}</div>
        <div className="truncate text-xs text-muted-foreground">{booking.bookerEmail}</div>
      </div>
      <div className="truncate text-sm text-muted-foreground">{booking.eventTypeTitle}</div>
    </div>
  );
}

export function HandledByNerova({
  receipts,
  isLoading
}: Readonly<{
  receipts: { id: string; receipt: string | null; summary: string; createdAt: string; executedAt: string | null }[];
  isLoading: boolean;
}>) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <SparklesIcon className="size-4 text-primary" />
          <CardTitle>
            <Trans>Handled by Nerova</Trans>
          </CardTitle>
        </div>
        <CardDescription>
          <Trans>Receipts from the last 24 hours.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {isLoading ? (
          <>
            <Skeleton className="h-12 rounded-lg" />
            <Skeleton className="h-12 rounded-lg" />
          </>
        ) : receipts.length === 0 ? (
          <div className="rounded-lg border bg-muted/30 p-4 text-sm text-muted-foreground">
            <Trans>We will show each finished booking helper here.</Trans>
          </div>
        ) : (
          receipts.slice(0, 3).map((receipt) => (
            <div key={receipt.id} className="rounded-lg border p-3 text-sm">
              {receipt.receipt ?? receipt.summary}
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

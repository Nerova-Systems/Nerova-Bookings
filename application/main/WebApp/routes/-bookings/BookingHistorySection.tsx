import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";

import type { Schemas } from "@/shared/lib/api/client";

import { SectionTitle } from "./BookingDetailsSheetParts";

type BookingHistoryEntry = Schemas["BookingHistoryEntryResponse"];
type BookingHistoryEventType = Schemas["BookingHistoryEventType"];

export function BookingHistorySection({
  isLoading,
  entries
}: Readonly<{ isLoading: boolean; entries: BookingHistoryEntry[] }>) {
  const formatDate = useFormatDate();
  return (
    <section className="rounded-md border p-4">
      <SectionTitle>
        <Trans>History</Trans>
      </SectionTitle>
      {isLoading ? (
        <Skeleton className="h-10 w-full" />
      ) : entries.length === 0 ? (
        <span className="text-sm text-muted-foreground">
          <Trans>No history entries yet.</Trans>
        </span>
      ) : (
        <ol className="flex flex-col gap-3">
          {entries.map((entry) => (
            <li key={entry.id} className="flex items-start gap-3 border-l-2 border-muted pl-3">
              <div className="flex min-w-0 flex-col gap-0.5">
                <span className="text-sm font-medium">{getHistoryEventLabel(entry.eventType)}</span>
                <span className="text-xs text-muted-foreground">{formatDate(entry.occurredAt, true)}</span>
                {entry.actorUserId != null && (
                  <span className="text-xs text-muted-foreground">
                    <Trans>By {entry.actorUserId}</Trans>
                  </span>
                )}
              </div>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function getHistoryEventLabel(eventType: BookingHistoryEventType): string {
  switch (eventType) {
    case "Created":
      return t`Created`;
    case "Confirmed":
      return t`Confirmed`;
    case "Rejected":
      return t`Rejected`;
    case "Rescheduled":
      return t`Rescheduled`;
    case "Cancelled":
      return t`Cancelled`;
    case "NoShow":
      return t`Marked as no-show`;
    case "LocationChanged":
      return t`Location changed`;
    case "GuestAdded":
      return t`Guest added`;
    case "Reassigned":
      return t`Reassigned`;
    case "Rated":
      return t`Rated`;
    case "SeatReserved":
      return t`Seat reserved`;
    case "SeatReleased":
      return t`Seat released`;
    default:
      return eventType;
  }
}

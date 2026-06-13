import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";

import { api, type Schemas } from "@/shared/lib/api/client";

type Delivery = Schemas["BookingSideEffectDeliverySummaryResponse"];

type SideEffectDeliveriesPanelProps = Readonly<{
  eventTypeId: string;
  kind: "email" | "webhook";
}>;

export function SideEffectDeliveriesPanel({ eventTypeId, kind }: SideEffectDeliveriesPanelProps) {
  const { data, isLoading } = api.useQuery("get", "/api/event-types/{eventTypeId}/side-effect-deliveries", {
    params: { path: { eventTypeId } }
  });
  const deliveries = (data?.deliveries ?? []).filter((delivery) => delivery.kind === kind).slice(0, 5);

  return (
    <div className="grid gap-3 rounded-md border p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-medium">
          <Trans>Recent deliveries</Trans>
        </h3>
        <span className="text-xs text-muted-foreground">
          {kind === "email" ? <Trans>Email</Trans> : <Trans>Developer notification</Trans>}
        </span>
      </div>
      {isLoading ? (
        <p className="text-sm text-muted-foreground">
          <Trans>Loading deliveries...</Trans>
        </p>
      ) : deliveries.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          <Trans>No delivery attempts yet.</Trans>
        </p>
      ) : (
        <div className="grid gap-2">
          {deliveries.map((delivery) => (
            <DeliveryRow key={delivery.id} delivery={delivery} />
          ))}
        </div>
      )}
    </div>
  );
}

function DeliveryRow({ delivery }: Readonly<{ delivery: Delivery }>) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-md bg-muted/50 px-3 py-2 text-sm">
      <div className="min-w-0">
        <p className="truncate font-medium">{delivery.trigger}</p>
        <p className="text-xs text-muted-foreground">
          <Trans>Attempts</Trans>: {delivery.attempts}
        </p>
      </div>
      <Badge variant={deliveryStatusVariant(delivery.status)}>{deliveryStatusLabel(delivery.status)}</Badge>
    </div>
  );
}

function deliveryStatusVariant(status: string): "secondary" | "destructive" | "warning" | "outline" {
  switch (status) {
    case "sent":
      return "secondary";
    case "failed":
      return "destructive";
    case "pending":
      return "warning";
    default:
      return "outline";
  }
}

function deliveryStatusLabel(status: string) {
  switch (status) {
    case "sent":
      return t`Sent`;
    case "failed":
      return t`Failed`;
    case "pending":
      return t`Pending`;
    default:
      return status;
  }
}

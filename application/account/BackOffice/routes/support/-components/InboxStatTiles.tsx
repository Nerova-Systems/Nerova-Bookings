import type { MessageDescriptor } from "@lingui/core";

import { msg } from "@lingui/core/macro";
import { useLingui } from "@lingui/react";
import { ToggleGroup, ToggleGroupItem } from "@repo/ui/components/ToggleGroup";

import type { Schemas } from "@/shared/lib/api/client";

import { SupportTicketStatus } from "@/shared/lib/api/client";

interface TileDefinition {
  key: SupportTicketStatus;
  label: MessageDescriptor;
  countKey: keyof Schemas["AllTicketsCounts"];
  // Tailwind classes appended after the Toggle base so they win the cascade for background/text/ring,
  // including in the on-state. Active state is conveyed via `aria-pressed` and a ring-2 outline rather
  // than swapping background to the default `aria-pressed:bg-primary`.
  tileClass: string;
}

const TILES: TileDefinition[] = [
  {
    key: SupportTicketStatus.New,
    label: msg`New`,
    countKey: "new",
    tileClass: "bg-info/10 text-info ring-info/25 hover:bg-info/15 aria-pressed:bg-info/15 aria-pressed:text-info"
  },
  {
    key: SupportTicketStatus.AwaitingAgent,
    label: msg`Awaiting agent`,
    countKey: "awaitingAgent",
    tileClass:
      "bg-warning/10 text-warning ring-warning/25 hover:bg-warning/15 aria-pressed:bg-warning/15 aria-pressed:text-warning"
  },
  {
    key: SupportTicketStatus.AwaitingUser,
    label: msg`Awaiting user`,
    countKey: "awaitingUser",
    tileClass:
      "bg-primary/10 text-primary ring-primary/25 hover:bg-primary/15 aria-pressed:bg-primary/15 aria-pressed:text-primary"
  },
  {
    key: SupportTicketStatus.AwaitingInternal,
    label: msg`Awaiting internal`,
    countKey: "awaitingInternal",
    tileClass:
      "bg-muted text-muted-foreground ring-border hover:bg-muted/80 aria-pressed:bg-muted aria-pressed:text-muted-foreground"
  },
  {
    key: SupportTicketStatus.Resolved,
    label: msg`Resolved (24h)`,
    countKey: "resolvedLast24Hours",
    tileClass:
      "bg-success/10 text-success ring-success/25 hover:bg-success/15 aria-pressed:bg-success/15 aria-pressed:text-success"
  }
];

interface InboxStatTilesProps {
  counts: Schemas["AllTicketsCounts"] | undefined;
  selectedStatus: SupportTicketStatus | undefined;
  onSelect: (status: SupportTicketStatus | undefined) => void;
}

export function InboxStatTiles({ counts, selectedStatus, onSelect }: Readonly<InboxStatTilesProps>) {
  const { i18n } = useLingui();
  return (
    <ToggleGroup
      spacing={3}
      aria-label={i18n._(msg`Filter by status`)}
      value={selectedStatus ? [selectedStatus] : []}
      onValueChange={(values) => onSelect((values[values.length - 1] as SupportTicketStatus) ?? undefined)}
      className="grid w-full grid-cols-2 sm:grid-cols-3 lg:grid-cols-5"
    >
      {TILES.map((tile) => (
        <ToggleGroupItem
          key={tile.key}
          value={tile.key}
          className={`flex !h-auto !min-w-0 flex-col items-start gap-1 rounded-lg px-4 py-3 text-left ring-1 transition-colors ring-inset active:opacity-80 ${tile.tileClass} aria-pressed:ring-2`}
        >
          <span className="text-xs font-medium opacity-80">{i18n._(tile.label)}</span>
          <span className="text-2xl font-semibold tabular-nums">{counts?.[tile.countKey] ?? 0}</span>
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}

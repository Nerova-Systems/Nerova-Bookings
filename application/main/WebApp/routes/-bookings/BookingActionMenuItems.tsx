import type { ReactNode } from "react";

import { DropdownMenuItem, DropdownMenuLabel } from "@repo/ui/components/DropdownMenu";

import type { Schemas } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

type BookingActions = Schemas["BookingActionsResponse"];
type BookingAction = Schemas["BookingActionResponse"];
export type BookingActionKey = keyof BookingActions;

export type BookingActionMenuItem = {
  key: BookingActionKey;
  icon: ReactNode;
  label: ReactNode;
  variant?: "default" | "destructive";
  onSelect?: () => void;
};

export function BookingActionGroup({
  label,
  booking,
  items
}: Readonly<{ label: ReactNode; booking: BookingListItem; items: BookingActionMenuItem[] }>) {
  const visibleItems = items.filter((item) => booking.actions[item.key].visible);
  if (visibleItems.length === 0) {
    return null;
  }

  return (
    <>
      <DropdownMenuLabel>{label}</DropdownMenuLabel>
      {visibleItems.map((item) => (
        <BookingActionItem
          key={item.key}
          action={booking.actions[item.key]}
          icon={item.icon}
          label={item.label}
          trackingLabel={String(item.key)}
          variant={item.variant}
          onSelect={item.onSelect}
        />
      ))}
    </>
  );
}

export function BookingActionItem({
  action,
  icon,
  label,
  trackingLabel,
  variant = "default",
  onSelect
}: Readonly<{
  action: BookingAction;
  icon: ReactNode;
  label: ReactNode;
  trackingLabel: string;
  variant?: "default" | "destructive";
  onSelect?: () => void;
}>) {
  if (!action.visible) {
    return null;
  }

  return (
    <DropdownMenuItem
      disabled={!action.enabled}
      variant={variant}
      title={action.disabledReason ?? undefined}
      trackingLabel={trackingLabel}
      onClick={(event) => {
        event.stopPropagation();
        if (!action.enabled) return;
        onSelect?.();
      }}
    >
      {icon}
      <span className="flex min-w-0 flex-col gap-0.5">
        <span>{label}</span>
        {!action.enabled && action.disabledReason && (
          <span className="text-xs leading-snug text-muted-foreground">{action.disabledReason}</span>
        )}
      </span>
    </DropdownMenuItem>
  );
}

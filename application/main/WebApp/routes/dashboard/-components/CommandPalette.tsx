import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList
} from "@repo/ui/components/Command";
import { useNavigate } from "@tanstack/react-router";

export function CommandPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (v: boolean) => void }) {
  const navigate = useNavigate();

  const jump = (to: string) => {
    onOpenChange(false);
    navigate({ to });
  };

  return (
    <CommandDialog
      open={open}
      onOpenChange={onOpenChange}
      trackingTitle="Command palette"
      title={t`Command palette`}
      description={t`Search clients, bookings, services or jump to a screen`}
    >
      <CommandInput placeholder={t`Search clients, bookings, services…`} />
      <CommandList>
        <CommandEmpty>
          <Trans>No results found.</Trans>
        </CommandEmpty>
        <CommandGroup heading={t`Jump to`}>
          <CommandItem onSelect={() => jump("/dashboard")}>Activity feed</CommandItem>
          <CommandItem onSelect={() => jump("/dashboard/calendar")}>Calendar</CommandItem>
          <CommandItem onSelect={() => jump("/dashboard/services")}>Services</CommandItem>
          <CommandItem onSelect={() => jump("/dashboard/clients")}>Clients</CommandItem>
          <CommandItem onSelect={() => jump("/dashboard/analytics")}>Analytics</CommandItem>
        </CommandGroup>
        <CommandGroup heading={t`Actions`}>
          <CommandItem>New manual booking</CommandItem>
          <CommandItem>New service</CommandItem>
          <CommandItem>Confirm Liam&apos;s 09:00 booking</CommandItem>
          <CommandItem>Send payment link to Aisha Patel</CommandItem>
        </CommandGroup>
      </CommandList>
    </CommandDialog>
  );
}

import { Trans } from "@lingui/react/macro";

import { money, type Client } from "@/shared/lib/appointmentsApi";

interface ClientDrawerAppointmentsProps {
  appointments: Client["appointmentHistory"];
}

export function ClientDrawerAppointments({ appointments }: ClientDrawerAppointmentsProps) {
  return (
    <div className="grid gap-2">
      {appointments.map((appointment) => (
        <div
          key={appointment.id}
          className="grid gap-3 border-b border-border py-3 text-sm last:border-b-0 md:grid-cols-[9rem_minmax(0,1fr)_8rem_8rem]"
        >
          <div>
            <div className="font-mono text-[12px] text-muted-foreground">{formatDateTime(appointment.startAt)}</div>
            <div className="mt-1 text-[12px] text-muted-foreground">{appointment.publicReference}</div>
          </div>
          <div className="min-w-0">
            <div className="truncate font-medium">{appointment.serviceName}</div>
            <div className="mt-1 truncate text-[12px] text-muted-foreground">{appointment.location}</div>
          </div>
          <div>
            <div className="text-[12px] font-medium">{formatEnum(appointment.status)}</div>
            <div className="mt-1 text-[12px] text-muted-foreground">{formatEnum(appointment.paymentStatus)}</div>
          </div>
          <div className="text-left md:text-right">
            <div className="font-mono">{money(appointment.priceCents)}</div>
            <div className="mt-1 text-[12px] text-muted-foreground">{formatSource(appointment.source)}</div>
          </div>
        </div>
      ))}
      {appointments.length === 0 && (
        <div className="rounded-lg border border-dashed border-border px-4 py-10 text-center text-sm text-muted-foreground">
          <Trans>No appointment history yet.</Trans>
        </div>
      )}
    </div>
  );
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

function formatEnum(value: string) {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function formatSource(value: string) {
  if (value === "PublicBookingPage") return "Public booking";
  if (value === "WhatsAppFlow") return "WhatsApp flow";
  return value;
}

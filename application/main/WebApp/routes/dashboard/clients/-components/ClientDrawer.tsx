import { Trans } from "@lingui/react/macro";
import { CalendarDaysIcon, GiftIcon, UserRoundIcon, XIcon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";

import type { Client } from "@/shared/lib/appointmentsApi";

import { ClientDrawerAppointments } from "./ClientDrawerAppointments";
import { ClientDrawerInformation, toClientForm } from "./ClientDrawerInformation";
import { ClientDrawerLoyalty } from "./ClientDrawerLoyalty";

type DrawerTab = "information" | "appointments" | "loyalty";

interface ClientDrawerProps {
  client: Client;
  pending: boolean;
  error?: Error | null;
  onClose: () => void;
  onSave: (request: {
    name: string;
    phone: string;
    email: string;
    status: string;
    alert?: string;
    internalNote?: string;
  }) => void;
}

export function ClientDrawer({ client, pending, error, onClose, onSave }: ClientDrawerProps) {
  const [activeTab, setActiveTab] = useState<DrawerTab>("information");
  const [form, setForm] = useState(() => toClientForm(client));
  const appointments = useMemo(
    () =>
      [...client.appointmentHistory].sort(
        (left, right) => new Date(right.startAt).getTime() - new Date(left.startAt).getTime()
      ),
    [client.appointmentHistory]
  );

  useEffect(() => {
    setForm(toClientForm(client));
  }, [client]);

  const submit = () => {
    onSave({
      name: form.name.trim(),
      phone: form.phone.trim(),
      email: form.email.trim(),
      status: form.status,
      alert: form.alert.trim() || undefined,
      internalNote: form.internalNote.trim() || undefined
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-foreground/20 backdrop-blur-[2px]">
      <aside className="flex h-full w-full flex-col border-l border-border bg-background shadow-2xl md:w-[60vw] md:max-w-[960px]">
        <header className="flex shrink-0 items-start gap-4 border-b border-border px-5 py-4">
          <div className="flex size-11 shrink-0 items-center justify-center rounded-full bg-foreground font-display text-sm font-semibold text-background">
            {client.initials}
          </div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="truncate font-display text-xl font-semibold">{client.name}</h2>
              <span className="rounded bg-muted px-1.5 py-0.5 text-[11px] font-medium text-muted-foreground">
                {client.status}
              </span>
            </div>
            <div className="mt-1 flex flex-wrap gap-x-3 gap-y-1 font-mono text-[12px] text-muted-foreground">
              <span>{client.phone}</span>
              <span>{client.email || "No email"}</span>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="ml-auto rounded-md p-2 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
            aria-label="Close client profile"
          >
            <XIcon className="size-4" />
          </button>
        </header>

        <div className="grid shrink-0 grid-cols-4 border-b border-border">
          <Metric label="Visits" value={String(client.visits)} />
          <Metric label="Lifetime" value={client.lifetime} />
          <Metric label="Last visit" value={client.lastVisit} />
          <Metric label="No-show" value={String(client.noShowCount)} />
        </div>

        <nav className="flex shrink-0 gap-1 border-b border-border px-5 py-2">
          <TabButton active={activeTab === "information"} onClick={() => setActiveTab("information")}>
            <UserRoundIcon className="size-3.5" />
            <Trans>Information</Trans>
          </TabButton>
          <TabButton active={activeTab === "appointments"} onClick={() => setActiveTab("appointments")}>
            <CalendarDaysIcon className="size-3.5" />
            <Trans>Appointments</Trans>
          </TabButton>
          <TabButton active={activeTab === "loyalty"} onClick={() => setActiveTab("loyalty")}>
            <GiftIcon className="size-3.5" />
            <Trans>Loyalty</Trans>
          </TabButton>
        </nav>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5">
          {activeTab === "information" && (
            <ClientDrawerInformation
              client={client}
              error={error}
              form={form}
              pending={pending}
              onChange={setForm}
              onReset={() => setForm(toClientForm(client))}
              onSubmit={submit}
            />
          )}
          {activeTab === "appointments" && <ClientDrawerAppointments appointments={appointments} />}
          {activeTab === "loyalty" && <ClientDrawerLoyalty />}
        </div>
      </aside>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="border-r border-border px-4 py-3 last:border-r-0">
      <div className="truncate font-display text-base font-semibold">{value}</div>
      <div className="mt-0.5 text-[11px] text-muted-foreground">{label}</div>
    </div>
  );
}

function TabButton({
  active,
  onClick,
  children
}: {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm transition-colors ${
        active ? "bg-foreground text-background" : "text-muted-foreground hover:bg-muted hover:text-foreground"
      }`}
    >
      {children}
    </button>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandShortcut
} from "@repo/ui/components/Command";
import { useNavigate } from "@tanstack/react-router";
import { useMemo, useState } from "react";

import {
  useAppointmentShell,
  type Appointment,
  type AppointmentShell,
  type Client,
  type IntegrationConnection,
  type Service
} from "@/shared/lib/appointmentsApi";

type SearchGroup = "Navigate" | "Appointments" | "Clients" | "Services" | "Payments" | "Apps";

interface SearchResult {
  id: string;
  group: SearchGroup;
  title: string;
  detail: string;
  keywords: string;
  to: string;
  appointmentId?: string;
  shortcut?: string;
}

const PAGES: SearchResult[] = [
  pageResult("activity", "Activity", "Operational feed and review queue", "/dashboard", "Workspace"),
  pageResult(
    "calendar",
    "Calendar",
    "Availability, bookings and external busy blocks",
    "/dashboard/calendar",
    "Workspace"
  ),
  pageResult("clients", "Clients", "Client database, notes and visit history", "/dashboard/clients", "Workspace"),
  pageResult("payments", "Payments", "Appointment deposits and Paystack status", "/dashboard/payments", "Business"),
  pageResult("services", "Services", "Service catalogue, prices and deposits", "/dashboard/services", "Business"),
  pageResult(
    "analytics",
    "Analytics",
    "Bookings, revenue, no-shows and service mix",
    "/dashboard/analytics",
    "Business"
  ),
  pageResult("apps", "Apps", "Google, Microsoft and Nango integrations", "/dashboard/apps", "Business")
];

export function CommandPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (v: boolean) => void }) {
  const [query, setQuery] = useState("");
  const navigate = useNavigate();
  const shellQuery = useAppointmentShell();
  const results = useMemo(() => buildResults(query, shellQuery.data), [query, shellQuery.data]);

  const jump = (item: SearchResult) => {
    if (item.appointmentId) sessionStorage.setItem("nerova:selectedAppointment", item.appointmentId);
    onOpenChange(false);
    setQuery("");
    navigate({ to: item.to });
  };

  return (
    <CommandDialog
      open={open}
      onOpenChange={onOpenChange}
      trackingTitle="Workspace search"
      title={t`Workspace search`}
      description={t`Search bookings, clients, services, payments, apps, or jump to a screen`}
      className="max-w-2xl"
    >
      <CommandInput
        value={query}
        onValueChange={setQuery}
        placeholder={t`Search Liam, overdue payments, Google Calendar, Full consultation...`}
      />
      <CommandList className="max-h-[28rem]">
        <CommandEmpty>
          <Trans>No results found.</Trans>
        </CommandEmpty>
        {groupResults(results).map(([group, items]) => (
          <CommandGroup key={group} heading={group}>
            {items.map((item) => (
              <CommandItem
                key={item.id}
                value={`${item.title} ${item.detail} ${item.keywords}`}
                onSelect={() => jump(item)}
              >
                <div className="min-w-0 flex-1">
                  <div className="truncate font-medium">{item.title}</div>
                  <div className="truncate text-xs text-muted-foreground">{item.detail}</div>
                </div>
                {item.shortcut && <CommandShortcut>{item.shortcut}</CommandShortcut>}
              </CommandItem>
            ))}
          </CommandGroup>
        ))}
      </CommandList>
    </CommandDialog>
  );
}

function buildResults(query: string, shell?: AppointmentShell) {
  const results = [
    ...PAGES,
    ...buildAppointmentResults(shell?.appointments ?? []),
    ...buildClientResults(shell?.clients ?? []),
    ...buildServiceResults(shell?.services ?? []),
    ...buildPaymentResults(shell?.appointments ?? []),
    ...buildAppResults(shell?.integrations ?? [])
  ];
  return rankResults(results, query).slice(0, 24);
}

function buildAppointmentResults(appointments: Appointment[]): SearchResult[] {
  return appointments.map((appointment) => ({
    id: `appointment-${appointment.id}`,
    group: "Appointments",
    title: `${appointment.name} - ${appointment.service}`,
    detail: `${appointment.dayGroup} at ${appointment.time} - ${appointment.statusLabel}`,
    keywords: `${appointment.phone} ${appointment.email} ${appointment.amount} ${appointment.channel}`,
    to: "/dashboard",
    appointmentId: appointment.id
  }));
}

function buildClientResults(clients: Client[]): SearchResult[] {
  return clients.map((client) => ({
    id: `client-${client.id}`,
    group: "Clients",
    title: client.name,
    detail: `${client.phone} - ${client.visits} visits - ${client.lifetime}`,
    keywords: `${client.email} ${client.status} ${client.alert ?? ""} ${client.internalNote ?? ""}`,
    to: "/dashboard/clients"
  }));
}

function buildServiceResults(services: Service[]): SearchResult[] {
  return services.map((service) => ({
    id: `service-${service.id}`,
    group: "Services",
    title: service.name,
    detail: `${service.duration} - ${service.price} - deposit ${service.deposit}`,
    keywords: `${service.modeLabel} ${service.location} bookings ${service.bookingsThisMonth}`,
    to: "/dashboard/services"
  }));
}

function buildPaymentResults(appointments: Appointment[]): SearchResult[] {
  return appointments
    .filter((appointment) => appointment.status !== "confirmed")
    .map((appointment) => ({
      id: `payment-${appointment.id}`,
      group: "Payments",
      title: `${appointment.statusLabel} - ${appointment.name}`,
      detail: `${appointment.amount} for ${appointment.service}`,
      keywords: `${appointment.phone} ${appointment.email} deposit paystack overdue pending payment`,
      to: "/dashboard",
      appointmentId: appointment.id
    }));
}

function buildAppResults(integrations: IntegrationConnection[]): SearchResult[] {
  return integrations.map((integration) => ({
    id: `app-${integration.provider}-${integration.capability}`,
    group: "Apps",
    title: `${integration.provider} ${integration.capability}`,
    detail: `${integration.status} via Nango`,
    keywords: "integration app calendar contacts email gmail outlook microsoft google nango",
    to: "/dashboard/apps"
  }));
}

function rankResults(results: SearchResult[], query: string) {
  const normalizedQuery = normalize(query);
  if (!normalizedQuery) return results;
  return results
    .map((result) => ({ result, score: scoreResult(result, normalizedQuery) }))
    .filter((item) => item.score > 0)
    .sort((a, b) => b.score - a.score || a.result.title.localeCompare(b.result.title))
    .map((item) => item.result);
}

function scoreResult(result: SearchResult, query: string) {
  const title = normalize(result.title);
  const detail = normalize(result.detail);
  const keywords = normalize(result.keywords);
  const haystack = `${title} ${detail} ${keywords}`;
  const tokens = query.split(" ").filter(Boolean);
  let score = title === query ? 100 : title.startsWith(query) ? 80 : title.includes(query) ? 60 : 0;
  if (detail.includes(query)) score += 35;
  if (keywords.includes(query)) score += 25;
  score += tokens.filter((token) => haystack.includes(token)).length * 12;
  return score;
}

function groupResults(results: SearchResult[]) {
  const order: SearchGroup[] = ["Navigate", "Appointments", "Clients", "Services", "Payments", "Apps"];
  return order
    .map((group) => [group, results.filter((result) => result.group === group)] as const)
    .filter(([, items]) => items.length > 0);
}

function pageResult(id: string, title: string, detail: string, to: string, shortcut: string): SearchResult {
  return { id: `page-${id}`, group: "Navigate", title, detail, keywords: `${title} ${detail}`, to, shortcut };
}

function normalize(value: string) {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim();
}

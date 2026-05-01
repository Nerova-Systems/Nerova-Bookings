/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import {
  CalendarIcon,
  CheckCircle2Icon,
  ChevronLeftIcon,
  ChevronRightIcon,
  CircleSlashIcon,
  Clock3Icon,
  FilterIcon,
  ListIcon,
  MoreHorizontalIcon,
  SearchIcon,
  UserPlusIcon,
  VideoIcon,
  XIcon
} from "lucide-react";
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { toast } from "sonner";

import {
  useAppointmentShell,
  useConfirmAppointment,
  useCreateTerminalPaymentIntent,
  useUpdateAppointmentStatus,
  type Appointment
} from "@/shared/lib/appointmentsApi";
import { businessDateKey, formatDayGroup, formatShortDate, formatTime } from "@/shared/lib/dateFormatting";

import { AppointmentPaymentBlock } from "../-components/AppointmentPaymentBlock";
import { useCmdk } from "../-components/CmdkContext";
import { StatusDot, statusTextClass } from "../-components/appointmentTypes";
import { WeekGrid } from "../calendar/-components/WeekGrid";

type BookingTab = "upcoming" | "unconfirmed" | "recurring" | "past" | "canceled";
type BookingView = "list" | "calendar";

interface BookingsSearch {
  tab?: BookingTab;
  view?: BookingView;
}

export const Route = createFileRoute("/dashboard/bookings/")({
  staticData: { trackingTitle: "Bookings" },
  validateSearch: (search: Record<string, unknown>): BookingsSearch => ({
    tab: isBookingTab(search.tab) ? search.tab : "upcoming",
    view: search.view === "calendar" ? "calendar" : "list"
  }),
  component: BookingsPage
});

const EMPTY_APPOINTMENTS: Appointment[] = [];
const DEFAULT_TIME_ZONE = "Africa/Johannesburg";

function BookingsPage() {
  const search = Route.useSearch();
  const navigate = useNavigate();
  const { setOpen: setCmdkOpen } = useCmdk();
  const shellQuery = useAppointmentShell();
  const confirmMutation = useConfirmAppointment();
  const statusMutation = useUpdateAppointmentStatus();
  const terminalPaymentMutation = useCreateTerminalPaymentIntent();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [customWeekStart, setCustomWeekStart] = useState<Date | null>(null);

  const tab = search.tab ?? "upcoming";
  const view = search.view ?? "list";
  const timeZone = shellQuery.data?.profile.timeZone ?? DEFAULT_TIME_ZONE;
  const appointments = shellQuery.data?.appointments ?? EMPTY_APPOINTMENTS;
  const filteredAppointments = useMemo(() => filterBookings(appointments, tab, timeZone), [appointments, tab, timeZone]);
  const selectedAppointment = filteredAppointments.find((appointment) => appointment.id === selectedId) ?? null;
  const weekStart = customWeekStart ?? startOfWeek(new Date());

  useEffect(() => {
    document.title = t`Bookings | Nerova`;
  }, []);

  useEffect(() => {
    const selectedAppointmentId = sessionStorage.getItem("nerova:selectedAppointment");
    if (selectedAppointmentId && appointments.some((appointment) => appointment.id === selectedAppointmentId)) {
      setSelectedId(selectedAppointmentId);
      sessionStorage.removeItem("nerova:selectedAppointment");
    }
  }, [appointments]);

  const setTab = (nextTab: BookingTab) => {
    setSelectedId(null);
    navigate({ to: "/dashboard/bookings", search: { ...search, tab: nextTab } });
  };

  const setView = (nextView: BookingView) => {
    navigate({ to: "/dashboard/bookings", search: { ...search, view: nextView } });
  };

  const moveWeek = (offset: number) => {
    const next = new Date(weekStart);
    next.setDate(weekStart.getDate() + offset * 7);
    setCustomWeekStart(next);
  };

  const confirmBooking = (id: string) =>
    confirmMutation.mutate(id, {
      onSuccess: () => {
        toast.success("Booking confirmed.");
        shellQuery.refetch();
      },
      onError: (error) => toast.error(error instanceof Error ? error.message : "Could not confirm booking.")
    });

  const updateStatus = (id: string, status: string) =>
    statusMutation.mutate(
      { id, status },
      {
        onSuccess: () => toast.success(`Booking marked ${status}.`),
        onError: (error) => toast.error(error instanceof Error ? error.message : "Could not update booking.")
      }
    );

  const createTerminalPayment = async (id: string) => {
    try {
      const intent = await terminalPaymentMutation.mutateAsync(id);
      toast.success("Terminal payment link created.");
      window.open(intent.terminalUrl, "_blank", "noopener,noreferrer");
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Could not create terminal payment link.");
    }
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden bg-[#0f0f0f] text-white">
      <header className="shrink-0 border-b border-white/10 px-5 py-4">
        <div className="flex flex-wrap items-center gap-3">
          <BookingTabs activeTab={tab} appointments={appointments} timeZone={timeZone} onChange={setTab} />
          <Button variant="outline" size="sm" className="border-white/15 bg-transparent text-white hover:bg-white/[0.08]">
            <FilterIcon className="size-4" />
            <Trans>Filter</Trans>
          </Button>
          <button
            type="button"
            onClick={() => setCmdkOpen(true)}
            className="ml-auto hidden h-10 min-w-[16rem] items-center gap-2 rounded-xl border border-white/10 bg-transparent px-3 text-sm text-white/65 transition-colors hover:border-white/20 md:flex"
          >
            <SearchIcon className="size-4" />
            <span className="text-left">
              <Trans>Search bookings</Trans>
            </span>
          </button>
          <div className="flex rounded-xl bg-white/[0.04] p-1">
            <IconToggle active={view === "list"} label="List view" onClick={() => setView("list")}>
              <ListIcon className="size-4" />
            </IconToggle>
            <IconToggle active={view === "calendar"} label="Calendar view" onClick={() => setView("calendar")}>
              <CalendarIcon className="size-4" />
            </IconToggle>
          </div>
        </div>
      </header>

      {shellQuery.isLoading ? (
        <div className="flex flex-1 items-center justify-center text-sm text-white/55">
          <Trans>Loading bookings...</Trans>
        </div>
      ) : view === "calendar" ? (
        <div
          className={`grid min-h-0 flex-1 overflow-hidden ${
            selectedAppointment ? "grid-cols-[minmax(0,1fr)_minmax(25rem,38vw)] max-xl:grid-cols-1" : "grid-cols-1"
          }`}
        >
          <main className="min-h-0 overflow-y-auto px-5 py-5">
            <div className="mb-4 flex flex-wrap items-center gap-2">
              <Button variant="outline" size="sm" className="border-white/15 bg-transparent text-white hover:bg-white/[0.08]" onClick={() => setCustomWeekStart(null)}>
                <Trans>Today</Trans>
              </Button>
              <button
                type="button"
                onClick={() => moveWeek(-1)}
                className="flex size-8 items-center justify-center rounded-lg border border-white/10 text-white/75 hover:bg-white/[0.08]"
                aria-label="Previous week"
              >
                <ChevronLeftIcon className="size-4" />
              </button>
              <button
                type="button"
                onClick={() => moveWeek(1)}
                className="flex size-8 items-center justify-center rounded-lg border border-white/10 text-white/75 hover:bg-white/[0.08]"
                aria-label="Next week"
              >
                <ChevronRightIcon className="size-4" />
              </button>
              <div className="ml-1 rounded-xl border border-white/10 px-3 py-2 text-sm font-semibold">{weekLabel(weekStart)}</div>
            </div>
            <WeekGrid
              appointments={filteredAppointments}
              blocks={shellQuery.data?.calendarBlocks ?? []}
              availabilityRules={shellQuery.data?.availabilityRules ?? []}
              closures={shellQuery.data?.closures ?? []}
              weekStart={weekStart}
              timeZone={timeZone}
              selectedAppointmentId={selectedId}
              onAppointmentSelect={setSelectedId}
            />
          </main>
          {selectedAppointment && (
            <BookingDrawer
              appointment={selectedAppointment}
              onClose={() => setSelectedId(null)}
              onConfirm={confirmBooking}
              onStatusChange={updateStatus}
              onCreateTerminalPayment={createTerminalPayment}
              isCreatingTerminalPayment={terminalPaymentMutation.isPending}
            />
          )}
        </div>
      ) : (
          <BookingsList
            tab={tab}
            appointments={filteredAppointments}
            onStatusChange={updateStatus}
            onSelect={(appointment) => {
            setSelectedId(appointment.id);
            setView("calendar");
          }}
        />
      )}
    </div>
  );
}

function BookingTabs({
  activeTab,
  appointments,
  timeZone,
  onChange
}: {
  activeTab: BookingTab;
  appointments: Appointment[];
  timeZone: string;
  onChange: (tab: BookingTab) => void;
}) {
  const tabs: { key: BookingTab; label: string }[] = [
    { key: "upcoming", label: "Upcoming" },
    { key: "unconfirmed", label: "Unconfirmed" },
    { key: "recurring", label: "Recurring" },
    { key: "past", label: "Past" },
    { key: "canceled", label: "Canceled" }
  ];
  const counts = Object.fromEntries(tabs.map((item) => [item.key, filterBookings(appointments, item.key, timeZone).length]));

  return (
    <div className="flex min-w-0 flex-wrap rounded-xl bg-white/[0.04] p-1">
      {tabs.map((item) => (
        <button
          key={item.key}
          type="button"
          onClick={() => onChange(item.key)}
          className={`rounded-lg px-3 py-2 text-sm font-semibold transition-colors ${
            activeTab === item.key ? "bg-[#101010] text-white shadow-[0_0_0_1px_rgba(255,255,255,0.08)]" : "text-white/75 hover:text-white"
          }`}
        >
          {item.label}
          {counts[item.key] > 0 && <span className="ml-2 text-xs text-white/45">{counts[item.key]}</span>}
        </button>
      ))}
    </div>
  );
}

function BookingsList({
  tab,
  appointments,
  onStatusChange,
  onSelect
}: {
  tab: BookingTab;
  appointments: Appointment[];
  onStatusChange: (id: string, status: string) => void;
  onSelect: (appointment: Appointment) => void;
}) {
  const groups = appointments.reduce<Record<string, Appointment[]>>((acc, appointment) => {
    if (!acc[appointment.dayGroup]) acc[appointment.dayGroup] = [];
    acc[appointment.dayGroup].push(appointment);
    return acc;
  }, {});

  return (
    <main className="flex-1 overflow-y-auto px-5 py-5">
      {appointments.length === 0 ? (
        <EmptyBookings tab={tab} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-white/10 bg-[#171717]">
          {Object.entries(groups).map(([day, items]) => (
            <section key={day}>
              <div className="border-b border-white/10 px-7 py-4 text-xs font-semibold tracking-[0.08em] text-white uppercase">
                {day === items[0]?.dayGroup && tab === "upcoming" ? "Next" : day}
              </div>
              {items.map((appointment) => (
                <BookingRow
                  key={appointment.id}
                  appointment={appointment}
                  onStatusChange={onStatusChange}
                  onSelect={() => onSelect(appointment)}
                />
              ))}
            </section>
          ))}
          <div className="flex items-center gap-2 border-t border-white/10 px-4 py-3 text-sm text-white">
            <select className="h-9 rounded-lg border border-white/15 bg-[#101010] px-3 outline-none">
              <option>10</option>
              <option>25</option>
            </select>
            <span>rows per page</span>
          </div>
        </div>
      )}
    </main>
  );
}

function BookingRow({
  appointment,
  onStatusChange,
  onSelect
}: {
  appointment: Appointment;
  onStatusChange: (id: string, status: string) => void;
  onSelect: () => void;
}) {
  const when = formatAppointmentWhen(appointment);

  return (
    <div className="grid grid-cols-[13rem_1fr_auto] gap-5 border-b border-white/5 px-7 py-6 last:border-b-0 max-lg:grid-cols-1">
      <button type="button" onClick={onSelect} className="text-left">
        <div className="text-base font-semibold text-white">{when.day}</div>
        <div className="mt-1 text-sm text-white/75">{when.timeRange}</div>
        <div className="mt-2 inline-flex items-center gap-1.5 text-sm font-semibold text-sky-400">
          <VideoIcon className="size-4" />
          {appointment.location}
        </div>
      </button>
      <button type="button" onClick={onSelect} className="min-w-0 text-left">
        <div className="truncate text-base font-semibold text-white">
          {appointment.duration} meeting between {appointment.name} and {appointment.service}
        </div>
        <div className="mt-1 text-sm text-white/85">{appointment.name}</div>
        <div className="mt-3 flex flex-wrap items-center gap-2 text-xs">
          <span className={`inline-flex items-center gap-1.5 ${statusTextClass(appointment.status)}`}>
            <StatusDot status={appointment.status} />
            {appointment.statusLabel}
          </span>
          <span className="rounded-md bg-white/[0.06] px-2 py-1 text-white/65">v{appointment.serviceVersionNumber}</span>
          <span className="rounded-md bg-white/[0.06] px-2 py-1 text-white/65">{appointment.amount}</span>
        </div>
      </button>
      <details className="relative justify-self-end">
        <summary className="flex size-10 cursor-pointer list-none items-center justify-center rounded-xl border border-white/15 text-white hover:bg-white/[0.08] [&::-webkit-details-marker]:hidden">
          <MoreHorizontalIcon className="size-4" />
        </summary>
        <div className="absolute top-11 right-0 z-30 w-64 overflow-hidden rounded-xl border border-white/10 bg-[#121212] py-2 shadow-2xl">
          <MenuButton onClick={() => toast.message("Reschedule booking is coming next.")} icon={<Clock3Icon className="size-4" />} label="Reschedule booking" />
          <MenuButton onClick={() => toast.message("Request reschedule is coming next.")} icon={<UserPlusIcon className="size-4" />} label="Request reschedule" />
          <MenuButton onClick={() => toast.message("Location editing is coming next.")} icon={<VideoIcon className="size-4" />} label="Edit location" />
          <div className="my-2 border-t border-white/10" />
          <MenuButton onClick={() => onStatusChange(appointment.id, "Completed")} icon={<CheckCircle2Icon className="size-4" />} label="Complete event" />
          <MenuButton onClick={() => onStatusChange(appointment.id, "NoShow")} icon={<CircleSlashIcon className="size-4" />} label="Mark as no-show" />
          <MenuButton onClick={() => onStatusChange(appointment.id, "Cancelled")} icon={<XIcon className="size-4" />} label="Cancel event" danger />
        </div>
      </details>
    </div>
  );
}

function BookingDrawer({
  appointment,
  onClose,
  onConfirm,
  onStatusChange,
  onCreateTerminalPayment,
  isCreatingTerminalPayment
}: {
  appointment: Appointment | null;
  onClose: () => void;
  onConfirm: (id: string) => void;
  onStatusChange: (id: string, status: string) => void;
  onCreateTerminalPayment: (id: string) => Promise<void>;
  isCreatingTerminalPayment: boolean;
}) {
  if (!appointment) {
    return (
      <aside className="hidden min-h-0 border-l border-white/10 bg-[#101010] px-8 py-10 text-white xl:block">
        <div className="flex h-full items-center justify-center rounded-2xl border border-dashed border-white/10 text-center text-sm text-white/45">
          <Trans>Select a booking on the calendar to view details.</Trans>
        </div>
      </aside>
    );
  }

  const when = formatAppointmentWhen(appointment);
  const canConfirm = !["confirmed", "completed", "cancelled", "no-show"].includes(appointment.status);

  return (
    <aside className="flex min-h-0 flex-col border-l border-white/10 bg-[#101010] text-white max-xl:fixed max-xl:inset-y-0 max-xl:right-0 max-xl:z-40 max-xl:w-[min(34rem,100vw)]">
      <div className="flex items-center justify-end gap-2 border-b border-white/10 px-6 py-4">
        <button type="button" className="flex size-9 items-center justify-center rounded-lg border border-white/15 hover:bg-white/[0.08]" onClick={onClose} aria-label="Close booking drawer">
          <XIcon className="size-4" />
        </button>
      </div>
      <div className="flex-1 overflow-y-auto px-7 py-6">
        <span className="inline-flex rounded-md bg-emerald-500/80 px-2 py-1 text-xs font-semibold text-white">{appointment.statusLabel}</span>
        <div className="mt-4 border-l-2 border-white/25 pl-4">
          <h2 className="font-display text-2xl leading-tight font-semibold">
            {appointment.duration} meeting between {appointment.name} and {appointment.service}
          </h2>
        </div>

        <DetailSection title="When">
          <div className="font-semibold">{when.longDay}</div>
          <div className="font-semibold">
            {when.timeRange} ({DEFAULT_TIME_ZONE})
          </div>
        </DetailSection>

        <DetailSection title="Who">
          <PersonLine name={appointment.name} detail={appointment.email || appointment.phone} badge="Client" />
          <PersonLine name="Colin Swart" detail="Host" badge="Host" />
        </DetailSection>

        <DetailSection title="Where">
          <div className="flex items-center gap-2 font-semibold">
            <VideoIcon className="size-4 text-white/60" />
            {appointment.location}
          </div>
        </DetailSection>

        <DetailSection title="Booking">
          <div className="grid gap-2 text-sm">
            <InfoRow label="Service" value={`${appointment.service} - v${appointment.serviceVersionNumber}`} />
            <InfoRow label="Payment" value={`${appointment.amount} - ${appointment.paymentStatus}`} />
            <InfoRow label="Reference" value={appointment.publicReference} />
          </div>
        </DetailSection>

        <AppointmentPaymentBlock
          appointment={appointment}
          onCreateTerminalPayment={onCreateTerminalPayment}
          isCreatingTerminalPayment={isCreatingTerminalPayment}
        />
      </div>
      <div className="flex items-center gap-2 border-t border-white/10 px-6 py-4">
        <Button variant="outline" size="sm" className="border-white/15 bg-transparent text-white hover:bg-white/[0.08]" onClick={() => toast.message("Join link handling is coming next.")}>
          <VideoIcon className="size-4" />
          <Trans>Join call</Trans>
        </Button>
        <Button size="sm" className="ml-auto" disabled={!canConfirm} onClick={() => onConfirm(appointment.id)}>
          <Trans>Confirm</Trans>
        </Button>
        <Button variant="outline" size="icon-sm" className="border-white/15 bg-transparent text-white hover:bg-white/[0.08]" onClick={() => onStatusChange(appointment.id, "Cancelled")}>
          <MoreHorizontalIcon className="size-4" />
        </Button>
      </div>
    </aside>
  );
}

function EmptyBookings({ tab }: { tab: BookingTab }) {
  const labels: Record<BookingTab, string> = {
    upcoming: "No upcoming bookings",
    unconfirmed: "No unconfirmed bookings",
    recurring: "No recurring bookings",
    past: "No past bookings",
    canceled: "No canceled bookings"
  };

  return (
    <div className="flex min-h-[34rem] items-center justify-center rounded-xl border border-dashed border-white/10 text-center">
      <div>
        <div className="mx-auto flex size-20 items-center justify-center rounded-full bg-white/[0.18]">
          <CalendarIcon className="size-10 text-white/80" />
        </div>
        <h2 className="mt-8 font-display text-2xl font-semibold">{labels[tab]}</h2>
        <p className="mt-4 max-w-xl text-base leading-7 text-white/75">
          <Trans>Bookings that match this view will show up here.</Trans>
        </p>
      </div>
    </div>
  );
}

function IconToggle({ active, label, onClick, children }: { active: boolean; label: string; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-label={label}
      onClick={onClick}
      className={`flex size-9 items-center justify-center rounded-lg transition-colors ${
        active ? "bg-[#101010] text-white shadow-[0_0_0_1px_rgba(255,255,255,0.08)]" : "text-white/65 hover:text-white"
      }`}
    >
      {children}
    </button>
  );
}

function MenuButton({ icon, label, danger, onClick }: { icon: ReactNode; label: string; danger?: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex w-full items-center gap-3 px-4 py-2 text-left text-sm font-semibold hover:bg-white/[0.06] ${
        danger ? "text-red-300" : "text-white/80"
      }`}
    >
      {icon}
      {label}
    </button>
  );
}

function DetailSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mt-7">
      <h3 className="mb-2 text-sm font-semibold text-white/45">{title}</h3>
      <div className="text-base text-white">{children}</div>
    </section>
  );
}

function PersonLine({ name, detail, badge }: { name: string; detail: string; badge: string }) {
  return (
    <div className="mb-3 flex items-center gap-3 last:mb-0">
      <div className="flex size-10 items-center justify-center rounded-full bg-white/10 text-sm font-semibold">{name.slice(0, 1)}</div>
      <div className="min-w-0">
        <div className="font-semibold">
          {name} <span className="ml-1 rounded bg-violet-400/20 px-1.5 py-0.5 text-xs text-violet-200">{badge}</span>
        </div>
        <div className="truncate text-sm text-white/65">{detail}</div>
      </div>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex gap-4">
      <span className="w-24 shrink-0 text-white/45">{label}</span>
      <span className="min-w-0 truncate font-semibold text-white">{value}</span>
    </div>
  );
}

function filterBookings(appointments: Appointment[], tab: BookingTab, timeZone: string) {
  const now = new Date();
  const todayKey = businessDateKey(now, timeZone);
  const filtered = appointments.filter((appointment) => {
    const start = new Date(appointment.startAt);
    const appointmentDay = businessDateKey(start, timeZone);
    if (tab === "canceled") return appointment.status === "cancelled";
    if (tab === "past") return appointment.status !== "cancelled" && (appointmentDay < todayKey || start.getTime() < now.getTime());
    if (tab === "unconfirmed") return appointment.status === "pending" || appointment.status === "payment-not-sent" || appointment.status === "payment-overdue";
    if (tab === "recurring") return false;
    return appointment.status !== "cancelled" && appointmentDay >= todayKey;
  });

  return filtered.toSorted((a, b) => {
    const diff = new Date(a.startAt).getTime() - new Date(b.startAt).getTime();
    return tab === "past" ? -diff : diff;
  });
}

function isBookingTab(value: unknown): value is BookingTab {
  return value === "upcoming" || value === "unconfirmed" || value === "recurring" || value === "past" || value === "canceled";
}

function startOfWeek(date: Date) {
  const next = new Date(date);
  const day = next.getDay() === 0 ? 7 : next.getDay();
  next.setDate(next.getDate() - day + 1);
  next.setHours(0, 0, 0, 0);
  return next;
}

function weekLabel(weekStart: Date) {
  const weekEnd = new Date(weekStart);
  weekEnd.setDate(weekStart.getDate() + 6);
  return `${formatShortDate(weekStart)} - ${formatShortDate(weekEnd)} ${weekEnd.getFullYear()}`;
}

function formatAppointmentWhen(appointment: Appointment) {
  const start = new Date(appointment.startAt);
  const end = new Date(appointment.endAt);
  return {
    day: formatDayGroup(start).replace(" 0", " "),
    longDay: new Intl.DateTimeFormat(undefined, { weekday: "long", month: "long", day: "numeric", year: "numeric" }).format(start),
    timeRange: `${formatTime(start)} - ${formatTime(end)}`
  };
}

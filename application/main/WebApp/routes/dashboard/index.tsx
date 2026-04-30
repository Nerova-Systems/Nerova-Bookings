import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { SearchIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import {
  useAppointmentShell,
  useConfirmAppointment,
  useCreateTerminalPaymentIntent,
  useUpdateAppointmentStatus,
  type Appointment
} from "@/shared/lib/appointmentsApi";
import { isTodayOrFutureInTimeZone } from "@/shared/lib/dateFormatting";

import { AppointmentDetail } from "./-components/AppointmentDetail";
import { AppointmentList, type FilterTab } from "./-components/AppointmentList";
import { ClientPanel } from "./-components/ClientPanel";
import { useCmdk } from "./-components/CmdkContext";

export const Route = createFileRoute("/dashboard/")({
  staticData: { trackingTitle: "Activity" },
  component: ActivityPage
});

const EMPTY_APPOINTMENTS: Appointment[] = [];
const DEFAULT_TIME_ZONE = "Africa/Johannesburg";

function ActivityPage() {
  const [selectedId, setSelectedId] = useState("1");
  const [activeTab, setActiveTab] = useState<FilterTab>("all");
  const { setOpen: setCmdkOpen } = useCmdk();
  const navigate = useNavigate();
  const shellQuery = useAppointmentShell();
  const confirmMutation = useConfirmAppointment();
  const statusMutation = useUpdateAppointmentStatus();
  const terminalPaymentMutation = useCreateTerminalPaymentIntent();

  const timeZone = shellQuery.data?.profile.timeZone ?? DEFAULT_TIME_ZONE;
  const appointments = shellQuery.data?.appointments ?? EMPTY_APPOINTMENTS;
  const activityAppointments = appointments
    .filter((appointment) => isTodayOrFutureInTimeZone(new Date(appointment.startAt), timeZone))
    .toSorted((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime());
  const selectedAppointment = activityAppointments.find((a) => a.id === selectedId) ?? activityAppointments[0];
  const selectedClient = shellQuery.data?.clients.find((client) => client.id === selectedAppointment?.clientId);

  useEffect(() => {
    if (activityAppointments.length > 0 && !activityAppointments.some((a) => a.id === selectedId)) {
      setSelectedId(activityAppointments[0].id);
    }
  }, [activityAppointments, selectedId]);

  useEffect(() => {
    const selectedAppointmentId = sessionStorage.getItem("nerova:selectedAppointment");
    if (selectedAppointmentId && activityAppointments.some((appointment) => appointment.id === selectedAppointmentId)) {
      setSelectedId(selectedAppointmentId);
      sessionStorage.removeItem("nerova:selectedAppointment");
    }
  }, [activityAppointments]);

  useEffect(() => {
    document.title = t`Activity | Nerova`;
  }, []);

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <header className="sticky top-0 z-20 flex shrink-0 items-center gap-4 border-b border-border bg-background px-7 py-3.5">
        <div className="flex min-w-0 flex-col gap-0.5">
          <h1 className="font-display text-[1.375rem] leading-tight">
            <Trans>Activity</Trans>
          </h1>
          <span className="text-[12.5px] text-muted-foreground">
            <Trans>Operational feed</Trans>
            {activityAppointments.length > 0 &&
              ` · ${activityAppointments.filter((appointment) => appointment.needsAction).length} items need review`}
          </span>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <button
            type="button"
            onClick={() => setCmdkOpen(true)}
            className="flex w-65 items-center gap-2 rounded-lg border border-transparent bg-muted px-2.5 py-1.5 text-[12.5px] text-muted-foreground transition-colors hover:border-border"
          >
            <SearchIcon className="size-3.5 shrink-0" />
            <span className="flex-1 text-left">
              <Trans>Search bookings, clients, services...</Trans>
            </span>
            <kbd className="rounded border border-border bg-background px-1.5 py-0.5 font-sans text-[10.5px] font-medium text-foreground">
              ⌘K
            </kbd>
          </button>
          <Button variant="outline" size="sm" onClick={() => navigate({ to: "/dashboard/calendar" })}>
            <Trans>View calendar</Trans>
          </Button>
          <Button size="sm">
            <Trans>New manual booking</Trans>
          </Button>
        </div>
      </header>

      <div className="grid min-h-0 flex-1 grid-cols-[380px_1fr_320px] overflow-hidden">
        {shellQuery.isLoading ? (
          <div className="col-span-3 flex items-center justify-center text-sm text-muted-foreground">
            <Trans>Loading appointments…</Trans>
          </div>
        ) : !selectedAppointment ? (
          <div className="col-span-3 flex items-center justify-center text-sm text-muted-foreground">
            <Trans>No appointments from today onward.</Trans>
          </div>
        ) : (
          <>
            <AppointmentList
              selectedId={selectedId}
              onSelect={setSelectedId}
              activeTab={activeTab}
              onTabChange={setActiveTab}
              appointments={activityAppointments}
              timeZone={timeZone}
            />
            <AppointmentDetail
              appointment={selectedAppointment}
              onConfirm={(id) =>
                confirmMutation.mutate(id, {
                  onSuccess: () => {
                    toast.success("Booking confirmed.");
                    shellQuery.refetch();
                  },
                  onError: (error) => toast.error(error instanceof Error ? error.message : "Could not confirm booking.")
                })
              }
              onStatusChange={(id, status) =>
                statusMutation.mutate(
                  { id, status },
                  {
                    onSuccess: () => toast.success(`Booking marked ${status}.`),
                    onError: (error) => toast.error(error instanceof Error ? error.message : "Could not update booking.")
                  }
                )
              }
              onCreateTerminalPayment={async (id) => {
                try {
                  const intent = await terminalPaymentMutation.mutateAsync(id);
                  toast.success("Terminal payment link created.");
                  window.open(intent.terminalUrl, "_blank", "noopener,noreferrer");
                } catch (error) {
                  toast.error(error instanceof Error ? error.message : "Could not create terminal payment link.");
                }
              }}
              isCreatingTerminalPayment={terminalPaymentMutation.isPending}
            />
            <ClientPanel appointment={selectedAppointment} client={selectedClient} />
          </>
        )}
      </div>
    </div>
  );
}

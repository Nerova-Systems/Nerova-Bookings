import type { Appointment, AppointmentStatus } from "@/shared/lib/appointmentsApi";

export type { Appointment, AppointmentStatus };

export function statusDotClass(status: AppointmentStatus) {
  if (status === "confirmed") return "bg-success";
  if (status === "pending") return "bg-warning";
  return "bg-destructive";
}

export function statusTextClass(status: AppointmentStatus) {
  if (status === "confirmed") return "text-success";
  if (status === "pending") return "text-warning";
  return "text-destructive";
}

export function StatusDot({ status }: { status: AppointmentStatus }) {
  return <span className={`inline-block size-[5px] rounded-full ${statusDotClass(status)}`} />;
}

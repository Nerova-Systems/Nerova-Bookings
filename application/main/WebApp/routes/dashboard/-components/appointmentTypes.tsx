export type AppointmentStatus = "pending" | "confirmed" | "payment-not-sent" | "payment-overdue";

export interface Appointment {
  id: string;
  dayGroup: string;
  time: string;
  duration: string;
  name: string;
  service: string;
  status: AppointmentStatus;
  statusLabel: string;
  channel: string;
  amount: string;
  needsAction: boolean;
}

export const APPOINTMENTS: Appointment[] = [
  {
    id: "1",
    dayGroup: "Today · 22 April",
    time: "09:00",
    duration: "60m",
    name: "Liam Botha",
    service: "Full consultation",
    status: "pending",
    statusLabel: "Awaiting confirmation",
    channel: "via WhatsApp Flow",
    amount: "R 450",
    needsAction: true
  },
  {
    id: "2",
    dayGroup: "Today · 22 April",
    time: "10:30",
    duration: "30m",
    name: "Thandi Khoza",
    service: "Express session",
    status: "payment-not-sent",
    statusLabel: "Payment link not sent",
    channel: "via WhatsApp Flow",
    amount: "R 220",
    needsAction: true
  },
  {
    id: "3",
    dayGroup: "Today · 22 April",
    time: "13:00",
    duration: "60m",
    name: "Pieter de Wet",
    service: "Follow-up visit",
    status: "confirmed",
    statusLabel: "Confirmed · paid",
    channel: "via WhatsApp Flow",
    amount: "R 150",
    needsAction: false
  },
  {
    id: "4",
    dayGroup: "Today · 22 April",
    time: "15:30",
    duration: "90m",
    name: "Group workshop · 4 attendees",
    service: "Group workshop",
    status: "confirmed",
    statusLabel: "Confirmed",
    channel: "via Public booking page",
    amount: "R 850",
    needsAction: false
  },
  {
    id: "5",
    dayGroup: "Tomorrow · 23 April",
    time: "09:30",
    duration: "60m",
    name: "Refilwe Mthembu",
    service: "Full consultation",
    status: "pending",
    statusLabel: "Awaiting confirmation",
    channel: "via WhatsApp Flow",
    amount: "R 450",
    needsAction: true
  },
  {
    id: "6",
    dayGroup: "Tomorrow · 23 April",
    time: "11:00",
    duration: "30m",
    name: "Marco Esposito",
    service: "Express session",
    status: "confirmed",
    statusLabel: "Confirmed · paid",
    channel: "via WhatsApp Flow",
    amount: "R 220",
    needsAction: false
  },
  {
    id: "7",
    dayGroup: "Wed · 24 April",
    time: "14:00",
    duration: "60m",
    name: "Aisha Patel",
    service: "Full consultation",
    status: "payment-overdue",
    statusLabel: "Payment overdue · invoice sent",
    channel: "via WhatsApp Flow",
    amount: "R 450",
    needsAction: true
  }
];

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

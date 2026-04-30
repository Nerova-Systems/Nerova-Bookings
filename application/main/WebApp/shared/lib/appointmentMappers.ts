import type {
  ApiAppointment,
  ApiClient,
  ApiService,
  ApiShell,
  Appointment,
  AppointmentShell,
  AppointmentStatus,
  Client,
  Service,
  ServicePaymentPolicy
} from "./appointmentContracts";

import { formatDayGroup, formatShortDate, formatTime, formatWholeNumber } from "./dateFormatting";

export function mapShell(shell: ApiShell): AppointmentShell {
  const appointments = shell.appointments.map(mapAppointment);
  return {
    profile: shell.profile,
    appointments,
    services: shell.services.map((service) => mapService(service, appointments)),
    categories: shell.categories,
    clients: shell.clients.map(mapClient),
    analytics: {
      bookings: shell.analytics.bookings,
      revenue: money(shell.analytics.revenueCents),
      clientsServed: shell.analytics.clientsServed,
      averageBookingValue: money(shell.analytics.averageBookingValueCents),
      noShowRate: `${shell.analytics.noShowRate}%`
    },
    integrations: shell.integrations
  };
}

function mapAppointment(appointment: ApiAppointment): Appointment {
  const start = new Date(appointment.startAt);
  const status = mapStatus(appointment.status, appointment.paymentStatus);
  return {
    id: appointment.id,
    publicReference: appointment.publicReference,
    clientId: appointment.clientId,
    serviceId: appointment.serviceId,
    serviceVersionId: appointment.serviceVersionId,
    serviceVersionNumber: appointment.serviceVersionNumber,
    dayGroup: formatDayGroup(start),
    time: formatTime(start),
    duration: `${appointment.durationMinutes}m`,
    name: appointment.clientName,
    phone: appointment.clientPhone,
    email: appointment.clientEmail,
    service: appointment.serviceName,
    status,
    statusLabel: statusLabel(status),
    paymentStatus: appointment.paymentStatus,
    paymentPolicy: appointment.paymentPolicy,
    paymentAmountCents: paymentAmountCents(appointment.paymentPolicy, appointment.priceCents, appointment.depositCents),
    channel: appointment.source === "Manual" ? "via Manual booking" : "via Public booking page",
    amount: money(appointment.priceCents),
    needsAction: status !== "confirmed",
    startAt: appointment.startAt,
    endAt: appointment.endAt,
    location: appointment.location,
    clientStatus: appointment.clientStatus,
    clientAlert: appointment.clientAlert,
    clientInternalNote: appointment.clientInternalNote
  };
}

function mapService(service: ApiService, appointments: Appointment[]): Service {
  return {
    id: service.id,
    categoryId: service.categoryId,
    name: service.name,
    mode: service.mode,
    modeLabel: service.mode === "physical" ? "Physical" : service.mode === "virtual" ? "Virtual" : "At client",
    duration: `${service.durationMinutes} min`,
    price: money(service.priceCents),
    deposit: service.depositCents > 0 ? money(service.depositCents) : "None",
    paymentPolicy: service.paymentPolicy,
    paymentPolicyLabel: paymentPolicyLabel(service.paymentPolicy, service.depositCents),
    location: service.location,
    bookingsThisMonth: appointments.filter((appointment) => appointment.service === service.name).length,
    archived: !service.isActive,
    durationMinutes: service.durationMinutes,
    priceCents: service.priceCents,
    depositCents: service.depositCents
  };
}

function mapClient(client: ApiClient): Client {
  return {
    id: client.id,
    initials: client.name
      .split(" ")
      .slice(0, 2)
      .map((namePart) => namePart[0])
      .join(""),
    name: client.name,
    phone: client.phone,
    email: client.email,
    visits: client.visitCount,
    lifetime: money(client.lifetimeSpendCents),
    lastVisit: client.lastVisitAt ? formatShortDate(new Date(client.lastVisitAt)) : "-",
    status: client.status,
    flag: client.alert ? "alert" : client.status === "Blocked" ? "blocked" : null,
    alert: client.alert,
    internalNote: client.internalNote,
    noShowCount: client.noShowCount,
    appointmentHistory: client.appointmentHistory
  };
}

function mapStatus(status: string, paymentStatus: string): AppointmentStatus {
  if (status === "Completed") return "completed";
  if (status === "Cancelled") return "cancelled";
  if (status === "NoShow") return "no-show";
  if (status === "Confirmed") return "confirmed";
  if (paymentStatus === "Failed") return "payment-overdue";
  if (paymentStatus === "Pending") return "payment-not-sent";
  return "pending";
}

function statusLabel(status: AppointmentStatus) {
  if (status === "confirmed") return "Confirmed";
  if (status === "completed") return "Completed";
  if (status === "cancelled") return "Cancelled";
  if (status === "no-show") return "No-show";
  if (status === "payment-not-sent") return "Payment link not sent";
  if (status === "payment-overdue") return "Payment overdue";
  return "Awaiting confirmation";
}

export function paymentPolicyLabel(paymentPolicy: ServicePaymentPolicy, depositCents = 0) {
  if (paymentPolicy === "DepositBeforeBooking")
    return depositCents > 0 ? "Deposit before booking" : "Deposit before booking";
  if (paymentPolicy === "FullPaymentBeforeBooking") return "Full payment before booking";
  if (paymentPolicy === "CollectAfterAppointment") return "Collect after appointment";
  return "No payment required";
}

function paymentAmountCents(paymentPolicy: ServicePaymentPolicy, priceCents: number, depositCents: number) {
  if (paymentPolicy === "NoPaymentRequired") return 0;
  if (paymentPolicy === "DepositBeforeBooking") return depositCents;
  return priceCents;
}

export function money(cents: number) {
  return `R ${formatWholeNumber(Math.round(cents / 100))}`;
}

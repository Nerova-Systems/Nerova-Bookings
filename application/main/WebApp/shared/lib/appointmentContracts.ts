export type AppointmentStatus = "pending" | "confirmed" | "payment-not-sent" | "payment-overdue" | "completed" | "cancelled" | "no-show";
export type ServicePaymentPolicy = "NoPaymentRequired" | "DepositBeforeBooking" | "FullPaymentBeforeBooking" | "CollectAfterAppointment";

export interface Appointment {
  id: string;
  publicReference: string;
  clientId: string;
  serviceId: string;
  dayGroup: string;
  time: string;
  duration: string;
  name: string;
  phone: string;
  email: string;
  service: string;
  status: AppointmentStatus;
  statusLabel: string;
  paymentStatus: string;
  paymentPolicy: ServicePaymentPolicy;
  paymentAmountCents: number;
  channel: string;
  amount: string;
  needsAction: boolean;
  startAt: string;
  endAt: string;
  location: string;
  clientStatus: string;
  clientAlert?: string;
  clientInternalNote?: string;
}

export interface Service {
  id: string;
  categoryId: string;
  name: string;
  mode: "physical" | "virtual" | "mobile";
  modeLabel: string;
  duration: string;
  price: string;
  deposit: string;
  paymentPolicy: ServicePaymentPolicy;
  paymentPolicyLabel: string;
  location: string;
  bookingsThisMonth: number;
  archived?: boolean;
  durationMinutes: number;
  priceCents: number;
  depositCents: number;
}

export interface ServiceCategory {
  id: string;
  name: string;
}

export interface Client {
  id: string;
  initials: string;
  name: string;
  phone: string;
  email: string;
  visits: number;
  lifetime: string;
  lastVisit: string;
  status: string;
  flag: "alert" | "overdue" | "blocked" | null;
  alert?: string;
  internalNote?: string;
}

export interface Analytics {
  bookings: number;
  revenue: string;
  clientsServed: number;
  averageBookingValue: string;
  noShowRate: string;
}

export interface IntegrationConnection {
  provider: string;
  capability: string;
  status: string;
  lastSyncedAt?: string;
}

export interface AppointmentShell {
  profile: BusinessProfile;
  appointments: Appointment[];
  services: Service[];
  categories: ServiceCategory[];
  clients: Client[];
  analytics: Analytics;
  integrations: IntegrationConnection[];
}

export interface BusinessProfile {
  name: string;
  slug: string;
  timeZone: string;
  address: string;
  publicBookingEnabled: boolean;
}

export interface ApiShell {
  profile: BusinessProfile;
  appointments: ApiAppointment[];
  services: ApiService[];
  categories: ServiceCategory[];
  clients: ApiClient[];
  analytics: {
    bookings: number;
    revenueCents: number;
    clientsServed: number;
    averageBookingValueCents: number;
    noShowRate: number;
  };
  integrations: IntegrationConnection[];
}

export interface ApiAppointment {
  id: string;
  publicReference: string;
  clientId: string;
  serviceId: string;
  startAt: string;
  endAt: string;
  clientName: string;
  clientPhone: string;
  clientEmail: string;
  serviceName: string;
  durationMinutes: number;
  priceCents: number;
  depositCents: number;
  paymentPolicy: ServicePaymentPolicy;
  status: string;
  paymentStatus: string;
  source: string;
  location: string;
  clientStatus: string;
  clientAlert?: string;
  clientInternalNote?: string;
}

export interface ApiService {
  id: string;
  categoryId: string;
  name: string;
  mode: "physical" | "virtual" | "mobile";
  durationMinutes: number;
  priceCents: number;
  depositCents: number;
  paymentPolicy: ServicePaymentPolicy;
  location: string;
  isActive: boolean;
}

export interface ApiClient {
  id: string;
  name: string;
  phone: string;
  email: string;
  status: string;
  alert?: string;
  internalNote?: string;
  visitCount: number;
  lifetimeSpendCents: number;
  lastVisitAt?: string;
}

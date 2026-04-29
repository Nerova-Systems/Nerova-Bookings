export type AppointmentStatus = "pending" | "confirmed" | "payment-not-sent" | "payment-overdue";

export interface Appointment {
  id: string;
  publicReference: string;
  dayGroup: string;
  time: string;
  duration: string;
  name: string;
  phone: string;
  email: string;
  service: string;
  status: AppointmentStatus;
  statusLabel: string;
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
  location: string;
  bookingsThisMonth: number;
  archived?: boolean;
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
  appointments: Appointment[];
  services: Service[];
  categories: ServiceCategory[];
  clients: Client[];
  analytics: Analytics;
  integrations: IntegrationConnection[];
}

export interface ApiShell {
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
  startAt: string;
  endAt: string;
  clientName: string;
  clientPhone: string;
  clientEmail: string;
  serviceName: string;
  durationMinutes: number;
  priceCents: number;
  depositCents: number;
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

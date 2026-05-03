export type AppointmentStatus =
  | "pending"
  | "confirmed"
  | "payment-not-sent"
  | "payment-overdue"
  | "completed"
  | "cancelled"
  | "no-show";
export type ServicePaymentPolicy =
  | "NoPaymentRequired"
  | "DepositBeforeBooking"
  | "FullPaymentBeforeBooking"
  | "CollectAfterAppointment";

export interface Appointment {
  id: string;
  publicReference: string;
  clientId: string;
  serviceId: string;
  serviceVersionId: string;
  serviceVersionNumber: number;
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
  meetUrl?: string | null;
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
  latestVersionNumber: number;
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
  noShowCount: number;
  appointmentHistory: ClientAppointmentHistory[];
}

export interface ClientAppointmentHistory {
  id: string;
  publicReference: string;
  startAt: string;
  endAt: string;
  serviceName: string;
  priceCents: number;
  depositCents: number;
  paymentPolicy: ServicePaymentPolicy;
  status: string;
  paymentStatus: string;
  source: string;
  location: string;
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
  lastSyncedAt?: string | null;
  ownerType: "Tenant" | "Location" | "StaffMember" | string;
  ownerId: string;
  externalConnectionId?: string | null;
}
export interface AvailabilityRule {
  id: string;
  dayOfWeek: string;
  startTime: string;
  endTime: string;
}
export interface HolidayCountry {
  code: string;
  name: string;
}
export interface PublicHoliday {
  id: string;
  countryCode: string;
  date: string;
  label: string;
  isOpen: boolean;
}
export interface HolidaySettings {
  countryCode: string;
  countries: HolidayCountry[];
  holidays: PublicHoliday[];
}
export interface BusinessClosure {
  id: string;
  startDate: string;
  endDate: string;
  label: string;
  type: "manual" | "publicHoliday";
}
export interface CalendarBlock {
  id: string;
  title: string;
  startAt: string;
  endAt: string;
  type: "manual" | "external";
}
export interface Slot {
  startAt: string;
  endAt: string;
}

export interface AppointmentShell {
  profile: BusinessProfile;
  appointments: Appointment[];
  services: Service[];
  categories: ServiceCategory[];
  clients: Client[];
  analytics: Analytics;
  integrations: IntegrationConnection[];
  availabilityRules: AvailabilityRule[];
  holidaySettings: HolidaySettings;
  closures: BusinessClosure[];
  calendarBlocks: CalendarBlock[];
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
  availabilityRules: AvailabilityRule[];
  holidaySettings: HolidaySettings;
  closures: BusinessClosure[];
  calendarBlocks: CalendarBlock[];
}

export interface ApiAppointment {
  id: string;
  publicReference: string;
  clientId: string;
  serviceId: string;
  serviceVersionId: string;
  serviceVersionNumber: number;
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
  meetUrl?: string | null;
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
  latestVersionNumber: number;
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
  noShowCount: number;
  lastVisitAt?: string;
  appointmentHistory: ClientAppointmentHistory[];
}

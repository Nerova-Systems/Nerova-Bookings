import {
  CalendarDaysIcon,
  CreditCardIcon,
  MailIcon,
  type LucideIcon,
  VideoIcon
} from "lucide-react";

export type AppCategory = "Analytics" | "AI & Automation" | "Calendar" | "Conferencing" | "CRM" | "Messaging" | "Payment" | "Other";
export type AppInstallState = "installed" | "available";

export interface AppCatalogItem {
  slug: string;
  name: string;
  provider: string;
  category: AppCategory;
  shortDescription: string;
  description: string;
  pricing: string;
  publisher: string;
  support: string;
  capabilities: string[];
  installState: AppInstallState;
  accentClassName: string;
  logoText: string;
  logoClassName: string;
  Icon: LucideIcon;
}

export const APP_CATALOG: AppCatalogItem[] = [
  {
    slug: "google-calendar",
    name: "Google Calendar",
    provider: "Google",
    category: "Calendar",
    shortDescription: "Sync bookings, conflict checks, and calendar events.",
    description:
      "Google Calendar keeps booking availability aligned with the calendars your business already uses. This connector will support event creation, conflict checks, and calendar-specific booking rules.",
    pricing: "Free",
    publisher: "Nerova",
    support: "support@nerova.app",
    capabilities: ["Add bookings", "Check conflicts", "Busy blocks"],
    installState: "installed",
    accentClassName: "from-blue-500/30 via-emerald-400/20 to-yellow-400/20",
    logoText: "31",
    logoClassName: "bg-[linear-gradient(135deg,#4285f4_0_50%,#34a853_50_70%,#fbbc04_70_85%,#ea4335_85%)] text-white",
    Icon: CalendarDaysIcon
  },
  {
    slug: "google-meet",
    name: "Google Meet",
    provider: "Google",
    category: "Conferencing",
    shortDescription: "Attach meeting links to virtual bookings.",
    description:
      "Google Meet will generate conferencing links for virtual services and keep meeting details visible on bookings, confirmations, and appointment reminders.",
    pricing: "Free with Google Workspace",
    publisher: "Nerova",
    support: "support@nerova.app",
    capabilities: ["Meeting links", "Virtual services", "Reminders"],
    installState: "available",
    accentClassName: "from-emerald-400/25 via-blue-400/20 to-yellow-300/20",
    logoText: "Meet",
    logoClassName: "bg-emerald-500 text-white",
    Icon: VideoIcon
  },
  {
    slug: "zoom-video",
    name: "Zoom Video",
    provider: "Zoom",
    category: "Conferencing",
    shortDescription: "Create Zoom rooms for virtual appointments.",
    description:
      "Zoom Video will let businesses create meeting links automatically for appointments that happen online, with meeting metadata stored alongside the booking.",
    pricing: "Free and paid Zoom plans",
    publisher: "Nerova",
    support: "support@nerova.app",
    capabilities: ["Meeting links", "Virtual services", "Client confirmations"],
    installState: "available",
    accentClassName: "from-sky-400/35 via-blue-500/20 to-white/10",
    logoText: "Z",
    logoClassName: "bg-blue-500 text-white",
    Icon: VideoIcon
  },
  {
    slug: "microsoft-calendar",
    name: "Microsoft Calendar",
    provider: "Microsoft",
    category: "Calendar",
    shortDescription: "Connect Outlook calendars for booking availability.",
    description:
      "Microsoft Calendar will provide Outlook event creation and availability checks for businesses that run scheduling through Microsoft 365.",
    pricing: "Requires Microsoft 365",
    publisher: "Nerova",
    support: "support@nerova.app",
    capabilities: ["Add bookings", "Check conflicts", "Busy blocks"],
    installState: "available",
    accentClassName: "from-blue-500/30 via-cyan-400/15 to-orange-400/20",
    logoText: "O",
    logoClassName: "bg-blue-600 text-white",
    Icon: CalendarDaysIcon
  },
  {
    slug: "gmail",
    name: "Gmail",
    provider: "Google",
    category: "Messaging",
    shortDescription: "Send booking email handoffs through Gmail.",
    description:
      "Gmail will support outbound booking messages, client follow-ups, and operational email handoffs from a connected business inbox.",
    pricing: "Free with Gmail or Google Workspace",
    publisher: "Nerova",
    support: "support@nerova.app",
    capabilities: ["Email handoffs", "Client follow-ups", "Booking messages"],
    installState: "available",
    accentClassName: "from-red-400/25 via-yellow-300/15 to-blue-400/20",
    logoText: "M",
    logoClassName: "bg-red-500 text-white",
    Icon: MailIcon
  },
  {
    slug: "microsoft-teams",
    name: "Microsoft Teams",
    provider: "Microsoft",
    category: "Conferencing",
    shortDescription: "Create Teams meeting links for appointments.",
    description:
      "Microsoft Teams will attach Teams calls to virtual services and keep conferencing details consistent across bookings and reminders.",
    pricing: "Requires Microsoft 365",
    publisher: "Nerova",
    support: "support@nerova.app",
    capabilities: ["Meeting links", "Virtual services", "Reminders"],
    installState: "available",
    accentClassName: "from-indigo-400/35 via-violet-400/20 to-white/10",
    logoText: "T",
    logoClassName: "bg-indigo-500 text-white",
    Icon: VideoIcon
  },
  {
    slug: "stripe",
    name: "Stripe",
    provider: "Stripe",
    category: "Payment",
    shortDescription: "Collect deposits and appointment payments.",
    description:
      "Stripe will provide an additional payment connector for deposits, full prepayment, and appointment settlement flows.",
    pricing: "Stripe transaction fees apply",
    publisher: "Nerova",
    support: "support@nerova.app",
    capabilities: ["Deposits", "Full payments", "Payment status"],
    installState: "available",
    accentClassName: "from-violet-400/30 via-indigo-400/20 to-white/10",
    logoText: "S",
    logoClassName: "bg-violet-600 text-white",
    Icon: CreditCardIcon
  },
  {
    slug: "paystack",
    name: "Paystack",
    provider: "Paystack",
    category: "Payment",
    shortDescription: "Manage local appointment payment flows.",
    description:
      "Paystack powers local payment collection for deposits, terminal flows, and appointment payment status inside Nerova.",
    pricing: "Paystack transaction fees apply",
    publisher: "Nerova",
    support: "support@nerova.app",
    capabilities: ["Deposits", "Terminal payments", "Payment status"],
    installState: "available",
    accentClassName: "from-cyan-400/30 via-blue-500/20 to-white/10",
    logoText: "P",
    logoClassName: "bg-sky-500 text-white",
    Icon: CreditCardIcon
  }
];

export function findApp(slug: string) {
  return APP_CATALOG.find((app) => app.slug === slug);
}

export function filterApps(query: string) {
  const normalizedQuery = query.trim().toLowerCase();
  if (!normalizedQuery) return APP_CATALOG;
  return APP_CATALOG.filter((app) =>
    [app.name, app.provider, app.category, app.shortDescription, ...app.capabilities].some((value) =>
      value.toLowerCase().includes(normalizedQuery)
    )
  );
}

export type LandingWorkflow = {
  readonly title: string;
  readonly description: string;
  readonly metric: string;
};

export type FixedFlowStep = {
  readonly title: string;
  readonly description: string;
  readonly label: string;
};

export type LandingContentBlock = {
  readonly title: string;
  readonly description: string;
};

export type IntegrationStage = {
  readonly stage: string;
  readonly title: string;
  readonly description: string;
  readonly items: readonly string[];
};

export type PaymentStage = {
  readonly title: string;
  readonly label: string;
  readonly description: string;
  readonly methods: readonly string[];
};

export const landingHero = {
  eyebrow: "Fixed WhatsApp flows for appointment businesses",
  title: "Bookings, reminders, and payments through structured WhatsApp flows",
  description:
    "Nerova is not an AI chatbot. It runs predictable WhatsApp-first flows for the full appointment lifecycle while your team manages calendars, services, staff, payments, and integrations from one workspace.",
  primaryCta: "Start free trial",
  secondaryCta: "See how it works"
} as const;

export const fixedFlowSteps: readonly FixedFlowStep[] = [
  {
    title: "Client message",
    description: "A customer starts from the channel they already use.",
    label: "WhatsApp"
  },
  {
    title: "Service selection",
    description: "They choose a configured service, staff member, and requirements.",
    label: "Fixed flow"
  },
  {
    title: "Calendar slot",
    description: "Availability is checked against the business calendar and team schedule.",
    label: "Availability"
  },
  {
    title: "Payment request",
    description: "Deposit or full payment is requested inside the booking sequence.",
    label: "Payment"
  },
  {
    title: "Reminder",
    description: "Structured confirmations, reminders, and reschedule prompts go out automatically.",
    label: "Reminder"
  },
  {
    title: "Confirmed visit",
    description: "The booking lands in operations with payment and client context attached.",
    label: "Booked"
  }
];

export const whyNerovaPoints: readonly LandingContentBlock[] = [
  {
    title: "WhatsApp-first, not web-form first",
    description: "Customers do not need to learn a new portal before they can book. Nerova starts where appointment conversations already happen."
  },
  {
    title: "Fixed flows, not an AI chatbot",
    description: "Every step is structured and controlled, so bookings, reminders, rescheduling, payments, and follow-ups stay predictable."
  },
  {
    title: "Built for non-technical operators",
    description: "The product focuses on running a professional service business, not developer webhooks or technical integration setup."
  }
];

export const workflowHighlights: readonly LandingWorkflow[] = [
  {
    title: "Cal.com-style calendar depth",
    description: "Detailed availability, staff schedules, buffers, service duration, booking rules, and calendar views without developer tooling.",
    metric: "Deep scheduling"
  },
  {
    title: "Configurable services",
    description: "Create services with pricing, deposits, duration, staff assignment, intake requirements, and booking constraints.",
    metric: "Custom services"
  },
  {
    title: "Operational staff views",
    description: "Give teams a clear daily workspace across appointments, client context, service notes, and payment state.",
    metric: "Team-ready"
  }
];

export const saasFoundationFeatures: readonly LandingContentBlock[] = [
  {
    title: "Account and workspace foundation",
    description: "Authentication, tenant workspaces, account settings, and secure session handling are already in place."
  },
  {
    title: "Users and team controls",
    description: "Invite users, manage roles, handle team membership, and recover deleted users from the account surface."
  },
  {
    title: "Subscription operations",
    description: "Plan selection, subscription changes, billing info, payment method updates, billing history, and PayFast-backed account billing."
  }
];

export const integrationStages: readonly IntegrationStage[] = [
  {
    stage: "V1",
    title: "Google tools",
    description: "The first integration set focuses on the tools appointment teams already use every day.",
    items: ["Google Calendar", "Google Contacts", "Gmail"]
  },
  {
    stage: "Roadmap",
    title: "Microsoft tools",
    description: "Microsoft Calendar, Outlook, and Contacts come after the Google rollout.",
    items: ["Microsoft Calendar", "Outlook", "Microsoft Contacts"]
  },
  {
    stage: "Future library",
    title: "Nango-powered connectors",
    description: "A broader integration library will let Nerova work with more existing business systems over time.",
    items: ["CRM", "Forms", "Analytics", "Accounting"]
  }
];

export const paymentStages: readonly PaymentStage[] = [
  {
    title: "Business subscriptions",
    label: "Available foundation",
    description: "Nerova account subscriptions use the PayFast-backed billing foundation.",
    methods: ["PayFast", "Plan changes", "Billing history"]
  },
  {
    title: "Client appointment payments",
    label: "Planned with Stitch",
    description: "Client deposits and appointment payments will run through the WhatsApp booking sequence using the planned Stitch integration.",
    methods: ["Google Pay", "Apple Pay", "Capitec Pay"]
  }
];

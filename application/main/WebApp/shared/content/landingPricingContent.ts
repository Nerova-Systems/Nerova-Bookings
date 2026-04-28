export type PricingPlan = {
  readonly name: string;
  readonly mappedPlan?: "Starter" | "Standard" | "Premium";
  readonly price: string;
  readonly description: string;
  readonly cta: string;
  readonly href?: string;
  readonly comingSoon?: boolean;
  readonly featured?: boolean;
};

export type PricingFeatureRow = {
  readonly feature: string;
  readonly solo: string;
  readonly studio: string;
  readonly business: string;
  readonly enterprise: string;
};

export const pricingPlans: readonly PricingPlan[] = [
  {
    name: "Solo",
    mappedPlan: "Starter",
    price: "R149",
    description: "For one operator who needs structured WhatsApp bookings and a professional service foundation.",
    cta: "Start free trial",
    href: "/signup"
  },
  {
    name: "Studio",
    mappedPlan: "Standard",
    price: "R299",
    description: "For small appointment businesses managing staff, services, reminders, and client payment flows.",
    cta: "Start free trial",
    href: "/signup",
    featured: true
  },
  {
    name: "Business",
    mappedPlan: "Premium",
    price: "R599",
    description: "For growing teams that need deeper scheduling, integrations, and operational controls.",
    cta: "Start free trial",
    href: "/signup"
  },
  {
    name: "Enterprise",
    price: "Custom",
    description: "Tailored workflows and custom operational datasets for specialized teams. Coming soon.",
    cta: "Coming soon",
    comingSoon: true
  }
];

export const pricingFeatureRows: readonly PricingFeatureRow[] = [
  { feature: "Staff users", solo: "1", studio: "Up to 5", business: "Up to 20", enterprise: "Custom" },
  { feature: "Configured services", solo: "10", studio: "50", business: "Unlimited", enterprise: "Custom" },
  {
    feature: "Fixed WhatsApp booking flows",
    solo: "Included",
    studio: "Included",
    business: "Included",
    enterprise: "Included"
  },
  {
    feature: "Account subscription billing",
    solo: "Included",
    studio: "Included",
    business: "Included",
    enterprise: "Planned"
  },
  { feature: "Client payment flows", solo: "Planned", studio: "Planned", business: "Planned", enterprise: "Planned" },
  { feature: "Google integrations", solo: "V1", studio: "V1", business: "V1", enterprise: "Custom" },
  { feature: "Custom business datasets", solo: "-", studio: "-", business: "Limited", enterprise: "Coming soon" }
];

export const faqs = [
  {
    question: "Is Nerova an AI chatbot?",
    answer:
      "No. Nerova uses fixed WhatsApp flows for booking, confirmations, reminders, rescheduling, payment prompts, and follow-ups. The goal is predictable operations, not open-ended chat."
  },
  {
    question: "Is Enterprise available now?",
    answer:
      "No. Enterprise is a roadmap plan for tailored workflows and custom datasets. Solo, Studio, and Business are the self-serve plans."
  },
  {
    question: "Will Nerova need a CMS later?",
    answer:
      "The landing content is static for now, but it is structured so a future open or self-hosted CMS can replace the data source."
  },
  {
    question: "What kind of businesses is Nerova for?",
    answer:
      "Nerova is built for appointment-based service businesses such as salons, clinics, consultants, fitness and wellness teams, repair services, studios, and solo operators."
  }
] as const;

import {
  CalendarCheck2Icon,
  ContactRoundIcon,
  KeyRoundIcon,
  MailIcon,
  MessageCircleIcon,
  PlugZapIcon,
  ReceiptIcon,
  RouteIcon,
  ShieldCheckIcon,
  UsersIcon
} from "lucide-react";

import {
  integrationStages,
  saasFoundationFeatures,
  whyNerovaPoints,
  workflowHighlights
} from "@/shared/content/landingContent";

const workflowIcons = [CalendarCheck2Icon, RouteIcon, UsersIcon] as const;
const foundationIcons = [KeyRoundIcon, UsersIcon, ReceiptIcon] as const;
const integrationIcons = [CalendarCheck2Icon, ContactRoundIcon, MailIcon] as const;

export function WhyNerovaSection() {
  return (
    <section id="why-nerova" className="border-b border-[#f3f4f6] bg-white">
      <div className="mx-auto grid max-w-7xl gap-12 px-6 py-20 lg:grid-cols-[0.8fr_1.2fr] lg:py-24">
        <div className="max-w-xl">
          <p className="text-sm font-semibold text-[#6b7280] uppercase">Why Nerova</p>
          <h2 className="mt-4 text-4xl leading-tight font-semibold md:text-5xl">
            Built for the way appointment businesses already communicate
          </h2>
          <p className="mt-5 text-lg leading-8 text-[#374151]">
            Nerova turns WhatsApp conversations into controlled business operations, without sending customers into a generic portal or an unpredictable bot.
          </p>
        </div>

        <div className="grid gap-4">
          {whyNerovaPoints.map((point, index) => (
            <article key={point.title} className="grid gap-4 rounded-2xl border border-[#e5e7eb] bg-[#f8f9fa] p-5 md:grid-cols-[3.5rem_1fr]">
              <div className="flex size-12 items-center justify-center rounded-2xl bg-white">
                {index === 0 && <MessageCircleIcon className="size-6" />}
                {index === 1 && <ShieldCheckIcon className="size-6" />}
                {index === 2 && <PlugZapIcon className="size-6" />}
              </div>
              <div>
                <h3 className="text-xl font-semibold">{point.title}</h3>
                <p className="mt-2 leading-7 text-[#374151]">{point.description}</p>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

export function WorkflowSection() {
  return (
    <section id="product" className="mx-auto max-w-7xl px-6 py-20 lg:py-24">
      <div className="grid gap-12 lg:grid-cols-[0.78fr_1.22fr]">
        <div className="max-w-lg">
          <p className="text-sm font-semibold text-[#6b7280] uppercase">Product</p>
          <h2 className="mt-4 text-4xl leading-tight font-semibold md:text-5xl">
            Calendar depth, service control, and team operations
          </h2>
          <p className="mt-5 text-lg leading-8 text-[#374151]">
            Nerova is the one-stop professional foundation for service businesses: detailed scheduling, configurable services, staff views, and client payment flows.
          </p>
        </div>

        <div className="grid gap-5">
          {workflowHighlights.map((workflow, index) => {
            const Icon = workflowIcons[index] ?? CalendarCheck2Icon;

            return (
              <article key={workflow.title} className="grid gap-5 rounded-2xl border border-[#e5e7eb] bg-white p-5 md:grid-cols-[4rem_1fr_auto] md:items-center">
                <div className="flex size-14 items-center justify-center rounded-2xl bg-[#f5f5f5] text-[#111111]">
                  <Icon className="size-6" />
                </div>
                <div>
                  <h3 className="text-xl font-semibold">{workflow.title}</h3>
                  <p className="mt-2 leading-7 text-[#374151]">{workflow.description}</p>
                </div>
                <span className="w-fit rounded-full bg-[#f5f5f5] px-3 py-1 text-sm font-medium text-[#374151]">{workflow.metric}</span>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}

export function FoundationSection() {
  return (
    <section className="border-y border-[#f3f4f6] bg-[#f8f9fa]">
      <div className="mx-auto max-w-7xl px-6 py-20 lg:py-24">
        <div className="mb-10 max-w-3xl">
          <p className="text-sm font-semibold text-[#6b7280] uppercase">SaaS foundation</p>
          <h2 className="mt-4 text-4xl leading-tight font-semibold md:text-5xl">
            The business foundation is already part of the platform
          </h2>
          <p className="mt-5 text-lg leading-8 text-[#374151]">
            The appointment product builds on account, tenant, team, and subscription capabilities already present in the application.
          </p>
        </div>

        <div className="grid gap-5 lg:grid-cols-3">
          {saasFoundationFeatures.map((feature, index) => {
            const Icon = foundationIcons[index] ?? KeyRoundIcon;

            return (
              <article key={feature.title} className="rounded-2xl border border-[#e5e7eb] bg-white p-6">
                <div className="mb-8 flex size-12 items-center justify-center rounded-2xl bg-[#f5f5f5]">
                  <Icon className="size-6" />
                </div>
                <h3 className="text-xl font-semibold">{feature.title}</h3>
                <p className="mt-3 leading-7 text-[#374151]">{feature.description}</p>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}

export function IntegrationsSection() {
  return (
    <section id="integrations" className="mx-auto max-w-7xl px-6 py-20 lg:py-24">
      <div className="grid gap-12 lg:grid-cols-[0.85fr_1.15fr]">
        <div className="flex max-w-xl flex-col justify-center gap-5">
          <p className="text-sm font-semibold text-[#6b7280] uppercase">Integrations</p>
          <h2 className="text-4xl leading-tight font-semibold md:text-5xl">
            Work with the tools your business already uses
          </h2>
          <p className="text-lg leading-8 text-[#374151]">
            Integrations are central to Nerova. The first rollout starts with Google tools, then expands to Microsoft and a broader Nango-powered library.
          </p>
        </div>

        <div className="grid gap-4">
          {integrationStages.map((stage, index) => {
            const Icon = integrationIcons[index] ?? PlugZapIcon;

            return (
              <article key={stage.title} className="rounded-2xl border border-[#e5e7eb] bg-[#f8f9fa] p-5">
                <div className="mb-5 flex items-start justify-between gap-4">
                  <div className="flex items-center gap-3">
                    <div className="flex size-11 items-center justify-center rounded-2xl bg-white">
                      <Icon className="size-5" />
                    </div>
                    <div>
                      <p className="text-xs font-semibold text-[#6b7280] uppercase">{stage.stage}</p>
                      <h3 className="text-xl font-semibold">{stage.title}</h3>
                    </div>
                  </div>
                </div>
                <p className="leading-7 text-[#374151]">{stage.description}</p>
                <div className="mt-5 flex flex-wrap gap-2">
                  {stage.items.map((item) => (
                    <span key={item} className="rounded-full bg-white px-3 py-1.5 text-sm font-medium text-[#374151]">
                      {item}
                    </span>
                  ))}
                </div>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}

import type { LucideIcon } from "lucide-react";

import { useIsAuthenticated } from "@repo/infrastructure/auth/hooks";
import { Link } from "@repo/ui/components/Link";
import { CalendarDaysIcon, CreditCardIcon, MessageCircleIcon, RouteIcon, ShieldCheckIcon } from "lucide-react";

import { fixedFlowSteps, landingHero } from "@/shared/content/landingContent";

import { TrialLink } from "./LandingLinks";

const heroMetrics: readonly { readonly label: string; readonly value: string; readonly Icon: LucideIcon }[] = [
  { label: "Flow type", value: "Fixed", Icon: RouteIcon },
  { label: "Client channel", value: "WhatsApp", Icon: MessageCircleIcon },
  { label: "Payment step", value: "Planned", Icon: CreditCardIcon }
];

export function LandingHero() {
  const isAuthenticated = useIsAuthenticated();

  return (
    <section className="relative isolate overflow-hidden border-b border-[#e5e7eb] bg-[#f8f9fa]">
      <div className="pointer-events-none absolute inset-0 bg-[linear-gradient(to_right,#e5e7eb_1px,transparent_1px),linear-gradient(to_bottom,#e5e7eb_1px,transparent_1px)] bg-[size:72px_72px] opacity-45" />
      <div className="mx-auto grid min-h-[calc(100svh-6rem)] max-w-7xl items-center gap-10 px-6 py-10 lg:grid-cols-[0.84fr_1.16fr] lg:py-12">
        <div className="relative z-10 flex max-w-2xl flex-col gap-8">
          <div className="flex flex-col gap-5">
            <p className="w-fit rounded-full border border-[#d1d5db] bg-white/80 px-3 py-1 text-sm font-medium text-[#374151] shadow-[0_1px_2px_rgba(17,17,17,0.04)]">
              {landingHero.eyebrow}
            </p>
            <div className="flex flex-col gap-4">
              <p className="text-3xl font-semibold text-[#111111]">Nerova</p>
              <h1 className="max-w-3xl text-5xl leading-[1.03] font-semibold text-balance md:text-6xl">
                {landingHero.title}
              </h1>
            </div>
            <p className="max-w-xl text-lg leading-8 text-[#374151]">{landingHero.description}</p>
            <div className="grid max-w-xl gap-3 sm:grid-cols-2">
              <div className="flex items-start gap-3 rounded-2xl border border-[#e5e7eb] bg-white/85 p-4">
                <ShieldCheckIcon className="mt-0.5 size-5 shrink-0" />
                <p className="text-sm leading-6 text-[#374151]">
                  Not an AI chatbot. No unpredictable free-form assistant behavior.
                </p>
              </div>
              <div className="flex items-start gap-3 rounded-2xl border border-[#e5e7eb] bg-white/85 p-4">
                <CalendarDaysIcon className="mt-0.5 size-5 shrink-0" />
                <p className="text-sm leading-6 text-[#374151]">
                  Structured booking, reminder, reschedule, payment, and follow-up flows.
                </p>
              </div>
            </div>
          </div>

          <div className="flex flex-col gap-3 sm:flex-row">
            {isAuthenticated ? (
              <Link href="/dashboard" variant="button-primary" underline={false} className="h-12 px-6">
                Go to app
              </Link>
            ) : (
              <>
                <TrialLink>{landingHero.primaryCta}</TrialLink>
                <Link href="#why-nerova" variant="button-secondary" underline={false} className="h-12 px-6">
                  {landingHero.secondaryCta}
                </Link>
              </>
            )}
          </div>
        </div>

        <div className="relative z-10">
          <HeroOperationsMockup />
        </div>
      </div>
    </section>
  );
}

function HeroOperationsMockup() {
  return (
    <div className="rounded-2xl border border-[#d1d5db] bg-white p-4 shadow-[0_28px_90px_rgba(17,17,17,0.16)] md:p-6">
      <div className="mb-5 flex items-center justify-between border-b border-[#f3f4f6] pb-4">
        <div>
          <p className="text-sm font-semibold">Fixed WhatsApp flow</p>
          <p className="text-sm text-[#6b7280]">From first message to confirmed appointment</p>
        </div>
        <span className="rounded-full bg-[#111111] px-3 py-1 text-xs font-semibold text-white">Structured</span>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        {fixedFlowSteps.map((step, index) => (
          <div key={step.title} className="rounded-xl border border-[#f3f4f6] bg-[#f8f9fa] p-4">
            <div className="mb-3 flex items-center justify-between gap-3">
              <span className="flex size-8 items-center justify-center rounded-full bg-[#111111] text-sm font-semibold text-white">
                {index + 1}
              </span>
              <span className="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-[#374151]">
                {step.label}
              </span>
            </div>
            <p className="text-sm font-semibold">{step.title}</p>
            <p className="mt-1 text-sm leading-6 text-[#6b7280]">{step.description}</p>
          </div>
        ))}
      </div>

      <div className="mt-4 grid gap-3 md:grid-cols-3">
        {heroMetrics.map(({ label, value, Icon }) => (
          <div key={label} className="rounded-xl bg-[#f5f5f5] p-4">
            <div className="mb-4 flex items-center justify-between">
              <span className="text-sm text-[#6b7280]">{label}</span>
              <Icon className="size-4" />
            </div>
            <p className="text-xl font-semibold">{value}</p>
          </div>
        ))}
      </div>
    </div>
  );
}

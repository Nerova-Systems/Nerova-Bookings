import { CheckIcon, CreditCardIcon, ReceiptIcon } from "lucide-react";

import { paymentStages } from "@/shared/content/landingContent";
import { faqs } from "@/shared/content/landingPricingContent";

import { TrialLink } from "./LandingLinks";

export function PaymentsSection() {
  return (
    <section id="payments" className="border-y border-[#f3f4f6] bg-[#101010] text-white">
      <div className="mx-auto grid max-w-7xl gap-12 px-6 py-20 lg:grid-cols-[0.85fr_1.15fr] lg:py-24">
        <div className="max-w-xl">
          <p className="text-sm font-semibold text-[#a1a1aa] uppercase">Payments</p>
          <h2 className="mt-4 text-4xl leading-tight font-semibold md:text-5xl">
            Payments belong inside the booking sequence
          </h2>
          <p className="mt-5 text-lg leading-8 text-[#d4d4d8]">
            Nerova separates business subscription billing from client appointment payments, then brings client payment
            prompts into the fixed WhatsApp flow.
          </p>
        </div>

        <div className="grid gap-4">
          {paymentStages.map((stage, index) => (
            <article key={stage.title} className="rounded-2xl border border-white/10 bg-white/[0.06] p-6">
              <div className="mb-5 flex items-center justify-between gap-4">
                <div className="flex size-12 items-center justify-center rounded-2xl bg-white text-[#111111]">
                  {index === 0 ? <ReceiptIcon className="size-6" /> : <CreditCardIcon className="size-6" />}
                </div>
                <span className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-[#111111]">
                  {stage.label}
                </span>
              </div>
              <h3 className="text-2xl font-semibold">{stage.title}</h3>
              <p className="mt-3 leading-7 text-[#d4d4d8]">{stage.description}</p>
              <div className="mt-5 flex flex-wrap gap-2">
                {stage.methods.map((method) => (
                  <span
                    key={method}
                    className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1.5 text-sm font-medium"
                  >
                    <CheckIcon className="size-4" />
                    {method}
                  </span>
                ))}
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

export function EnterpriseSection() {
  return (
    <section className="mx-auto max-w-7xl px-6 py-20 lg:py-24">
      <div className="grid gap-6 lg:grid-cols-[0.85fr_1.15fr]">
        <div className="rounded-2xl bg-[#101010] p-8 text-white lg:p-10">
          <p className="mb-4 text-sm font-semibold text-[#a1a1aa] uppercase">Enterprise roadmap</p>
          <h2 className="text-3xl leading-tight font-semibold md:text-4xl">
            Custom operations for specialized teams. Coming soon.
          </h2>
          <p className="mt-5 leading-7 text-[#d4d4d8]">
            Enterprise is where Nerova will support tailored workflows and custom operational datasets for specialized
            appointment businesses.
          </p>
        </div>
        <div className="grid gap-4">
          {faqs.map((faq) => (
            <article key={faq.question} className="rounded-xl bg-[#f5f5f5] p-6">
              <h3 className="text-lg font-semibold">{faq.question}</h3>
              <p className="mt-2 leading-7 text-[#374151]">{faq.answer}</p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

export function FinalCtaSection() {
  return (
    <section className="mx-auto max-w-7xl px-6 pb-20">
      <div className="flex flex-col items-start justify-between gap-8 rounded-2xl bg-[#f5f5f5] p-8 md:flex-row md:items-center lg:p-12">
        <div className="max-w-2xl">
          <h2 className="text-3xl font-semibold md:text-4xl">
            Start with fixed WhatsApp bookings. Grow into full operations.
          </h2>
          <p className="mt-3 text-lg leading-8 text-[#374151]">
            Launch Nerova with the self-serve plan that fits your appointment business today.
          </p>
        </div>
        <TrialLink />
      </div>
    </section>
  );
}

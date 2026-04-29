import { Link } from "@repo/ui/components/Link";
import { CheckIcon, CircleIcon, SparklesIcon } from "lucide-react";

import { pricingFeatureRows, pricingPlans } from "@/shared/content/landingPricingContent";

export function PricingSection() {
  return (
    <section id="pricing" className="mx-auto max-w-7xl px-6 py-20 lg:py-24">
      <div className="mb-10 flex flex-col gap-4">
        <p className="text-sm font-semibold tracking-wide text-[#6b7280] uppercase">Plans</p>
        <h2 className="max-w-3xl text-4xl leading-tight font-semibold md:text-5xl">
          Pricing that follows your business
        </h2>
        <p className="max-w-2xl text-lg leading-8 text-[#374151]">
          Solo, Studio, and Business are self-serve plans. Enterprise is a future custom plan for specialized teams.
        </p>
      </div>

      <div className="overflow-hidden rounded-3xl border border-[#e5e7eb] bg-white shadow-[0_18px_60px_rgba(17,17,17,0.06)]">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[58rem] text-left">
            <thead>
              <tr>
                <th
                  scope="col"
                  className="w-[18rem] border-b border-[#e5e7eb] bg-[#f8f9fa] p-5 text-sm font-semibold text-[#6b7280]"
                >
                  Feature
                </th>
                {pricingPlans.map((plan) => (
                  <th
                    key={plan.name}
                    scope="col"
                    className="border-b border-l border-[#e5e7eb] p-5 align-top"
                    data-testid={`pricing-plan-${plan.name.toLowerCase()}`}
                  >
                    <div className="flex min-h-52 flex-col gap-4">
                      <div className="flex flex-col gap-2">
                        <div className="flex items-center justify-between gap-3">
                          <span className="text-xl font-semibold">{plan.name}</span>
                          {plan.comingSoon && (
                            <span className="rounded-full bg-[#f5f5f5] px-2.5 py-1 text-xs font-semibold text-[#374151]">
                              Coming soon
                            </span>
                          )}
                        </div>
                        <div className="flex items-end gap-1">
                          <span className="text-3xl font-semibold">{plan.price}</span>
                          {!plan.comingSoon && <span className="pb-1 text-sm font-normal text-[#6b7280]">/month</span>}
                        </div>
                        {plan.mappedPlan && (
                          <p className="text-xs font-medium text-[#898989]">{plan.mappedPlan} plan</p>
                        )}
                      </div>
                      <p className="text-sm leading-6 font-normal text-[#374151]">{plan.description}</p>
                      <PlanCta plan={plan} />
                    </div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {pricingFeatureRows.map((row) => (
                <tr key={row.feature} className="border-b border-[#f3f4f6] last:border-b-0">
                  <th scope="row" className="bg-[#f8f9fa] p-5 text-sm font-semibold text-[#111111]">
                    {row.feature}
                  </th>
                  <PricingCell value={row.solo} />
                  <PricingCell value={row.studio} />
                  <PricingCell value={row.business} />
                  <PricingCell value={row.enterprise} />
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}

function PlanCta({ plan }: { readonly plan: (typeof pricingPlans)[number] }) {
  if (!plan.href) {
    return (
      <span
        className="mt-auto inline-flex h-10 items-center justify-center rounded-md border border-[#e5e7eb] bg-[#f5f5f5] px-4 text-sm font-semibold text-[#6b7280]"
        aria-disabled="true"
      >
        {plan.cta}
      </span>
    );
  }

  return (
    <Link
      href={plan.href}
      variant={plan.featured ? "button-primary" : "button-secondary"}
      underline={false}
      className="mt-auto h-10 px-4"
    >
      {plan.cta}
    </Link>
  );
}

function PricingCell({ value }: { readonly value: string }) {
  const isIncluded = value === "Included";
  const isUnavailable = value === "-";
  const isComingSoon = value === "Coming soon";

  return (
    <td className="border-l border-[#e5e7eb] p-5 text-sm text-[#374151]">
      <span className="inline-flex items-center gap-2">
        {isIncluded && <CheckIcon className="size-4 text-[#111111]" />}
        {isUnavailable && <CircleIcon className="size-3 text-[#d1d5db]" />}
        {isComingSoon && <SparklesIcon className="size-4 text-[#111111]" />}
        <span>{value}</span>
      </span>
    </td>
  );
}

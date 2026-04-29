import { lazy } from "react";

import { EnterpriseSection, FinalCtaSection, PaymentsSection } from "./LandingConversionSections";
import { LandingHero } from "./LandingHero";
import { FoundationSection, IntegrationsSection, WhyNerovaSection, WorkflowSection } from "./LandingSections";
import { PricingSection } from "./PricingSection";

const PublicFooter = lazy(() => import("account/PublicFooter"));
const PublicNavigation = lazy(() => import("account/PublicNavigation"));

export function LandingPageContent() {
  return (
    <main className="min-h-screen bg-white text-[#111111]">
      <PublicNavigation />
      <LandingHero />
      <WhyNerovaSection />
      <WorkflowSection />
      <FoundationSection />
      <IntegrationsSection />
      <PaymentsSection />
      <PricingSection />
      <EnterpriseSection />
      <FinalCtaSection />
      <PublicFooter />
    </main>
  );
}

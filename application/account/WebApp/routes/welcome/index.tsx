import { loggedInPath } from "@repo/infrastructure/auth/constants";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";

import ErrorPage from "@/federated-modules/errorPages/ErrorPage";
import { HorizontalHeroLayout } from "@/shared/layouts/HorizontalHeroLayout";

import { AccountSetupForm } from "./-components/AccountSetupForm";
import { ProfileSetupForm } from "./-components/ProfileSetupForm";

export const Route = createFileRoute("/welcome/")({
  staticData: { trackingTitle: "Welcome" },
  component: WelcomePage,
  errorComponent: ErrorPage
});

type WelcomeStep = "account" | "profile";

function WelcomePage() {
  const { isAuthenticated, firstName, role, tenantName } = import.meta.user_info_env;
  const isOwner = role === "Owner";

  // Determine initial step based on what's completed
  const hasCompletedAccountSetup = !isOwner || !!tenantName;
  const hasCompletedProfileSetup = !!firstName;
  const initialStep: WelcomeStep = hasCompletedAccountSetup ? "profile" : "account";

  const [step, setStep] = useState<WelcomeStep>(initialStep);

  // If not authenticated, redirect to login
  if (!isAuthenticated) {
    window.location.href = "/login";
    return null;
  }

  // If fully onboarded, redirect to app
  const isFullyOnboarded = hasCompletedAccountSetup && hasCompletedProfileSetup;
  if (isFullyOnboarded) {
    window.location.href = loggedInPath;
    return null;
  }

  return (
    <HorizontalHeroLayout>
      {step === "account" ? <AccountSetupForm onComplete={() => setStep("profile")} /> : <ProfileSetupForm />}
    </HorizontalHeroLayout>
  );
}

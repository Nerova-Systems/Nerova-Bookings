import { useFeatureFlag } from "@repo/infrastructure/featureFlags/useFeatureFlag";

export type PaymentTimingChoice = "AfterOnly" | "Both";

export type WhatsAppFlowTierLimits = {
  staffSelectionInFlow: boolean;
  paymentTimingChoice: PaymentTimingChoice;
  multipleServicesInFlow: boolean;
  /** Maximum allowed custom pre-booking questions; -1 means unlimited. */
  maxCustomPreBookingQuestions: number;
};

/**
 * Returns per-tier limits for the WhatsApp Flows questionnaire.
 *
 * TODO(phase-6): Replace the stub below with a call to
 * GET /api/whatsapp-flows/tier-limits once that endpoint is added to the
 * main SCS. The server enforces these limits in TierLimits.cs; this hook
 * is the frontend read path so the questionnaire can show upgrade prompts
 * before the user encounters a server-side rejection.
 *
 * Until the endpoint lands, limits are returned as fully unlocked, which
 * preserves Phase 5 behaviour where the server is the sole enforcement point.
 * The only exception is when the whatsapp-flows-enabled flag is off (tenant
 * is on the free/starter tier) — all advanced options are locked in that case.
 */
export function useWhatsAppFlowTierLimits(): WhatsAppFlowTierLimits {
  const { enabled: isWhatsAppEnabled } = useFeatureFlag("whatsapp-flows-enabled");

  // If the WhatsApp Flows feature flag is off the tenant is on the free tier
  // and all advanced options are locked.
  if (!isWhatsAppEnabled) {
    return {
      staffSelectionInFlow: false,
      paymentTimingChoice: "AfterOnly",
      multipleServicesInFlow: false,
      maxCustomPreBookingQuestions: 0
    };
  }

  // TODO(phase-6): fetch real tier limits from GET /api/whatsapp-flows/tier-limits.
  // Returning all-unlocked defaults until that endpoint is available.
  return {
    staffSelectionInFlow: true,
    paymentTimingChoice: "Both",
    multipleServicesInFlow: true,
    maxCustomPreBookingQuestions: -1
  };
}

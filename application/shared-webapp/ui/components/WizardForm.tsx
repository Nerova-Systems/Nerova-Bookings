import { Trans, useLingui } from "@lingui/react/macro";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useState } from "react";

import { cn } from "../utils";
import { Button } from "./Button";
import { Steps } from "./Steps";

/**
 * Multi-step form shell with step navigation, progress indicator, and slot for each step's content.
 * Ported from cal.com `packages/ui/components/wizard/WizardForm.tsx` (cf2a55c).
 *
 * Deviation: cal.com's WizardForm uses URL-based step routing (Next.js pages).
 * This version is uncontrolled-or-controlled state-only; routing is the consumer's responsibility.
 */
interface WizardStep {
  /** Unique step key. */
  key: string;
  title?: React.ReactNode;
  description?: React.ReactNode;
  content: React.ReactNode;
  /** If true the "Next" button is disabled. */
  isValid?: boolean;
  /** Override the default "Next" label for this step. */
  nextLabel?: React.ReactNode;
  /** Override the default "Back" label for this step. */
  prevLabel?: React.ReactNode;
}

interface WizardFormProps {
  steps: WizardStep[];
  /** Controlled step index (0-based). */
  currentStep?: number;
  defaultStep?: number;
  onStepChange?: (step: number) => void;
  onComplete?: () => void;
  /** Custom complete/finish button label on the last step. */
  completeLabel?: React.ReactNode;
  hideProgress?: boolean;
  className?: string;
}

export function WizardForm({
  steps,
  currentStep: controlledStep,
  defaultStep = 0,
  onStepChange,
  onComplete,
  completeLabel,
  hideProgress,
  className
}: WizardFormProps) {
  const { t } = useLingui();
  const [internalStep, setInternalStep] = useState(defaultStep);

  const isControlled = controlledStep !== undefined;
  const step = isControlled ? controlledStep : internalStep;

  const setStep = (next: number) => {
    if (!isControlled) setInternalStep(next);
    onStepChange?.(next);
  };

  const current = steps[step];
  const isFirst = step === 0;
  const isLast = step === steps.length - 1;

  if (!current) return null;

  return (
    <div data-slot="wizard-form" className={cn("flex flex-col gap-6", className)}>
      {!hideProgress && (
        <div className="flex flex-col items-center gap-3">
          <Steps currentStep={step} maxSteps={steps.length} />
          {current.title && (
            <div className="text-center">
              <h2 className="text-lg font-semibold text-foreground">{current.title}</h2>
              {current.description && <p className="mt-1 text-sm text-muted-foreground">{current.description}</p>}
            </div>
          )}
        </div>
      )}

      <div>{current.content}</div>

      <div className="flex items-center justify-between gap-2">
        <Button
          variant="outline"
          onClick={() => setStep(step - 1)}
          disabled={isFirst}
          aria-label={t`Go to previous step`}
        >
          <ChevronLeftIcon data-icon="inline-start" />
          {current.prevLabel ?? <Trans>Back</Trans>}
        </Button>

        {isLast ? (
          <Button onClick={onComplete} disabled={current.isValid === false}>
            {completeLabel ?? <Trans>Finish</Trans>}
          </Button>
        ) : (
          <Button
            onClick={() => setStep(step + 1)}
            disabled={current.isValid === false}
            aria-label={t`Go to next step`}
          >
            {current.nextLabel ?? <Trans>Next</Trans>}
            <ChevronRightIcon data-icon="inline-end" />
          </Button>
        )}
      </div>
    </div>
  );
}

import { cn } from "../utils";
import { Steps } from "./Steps";

/**
 * Multi-step wizard layout with a header step indicator and content slot.
 * Ported from cal.com `packages/ui/components/layout/WizardLayout.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface WizardLayoutProps {
  currentStep: number;
  maxSteps: number;
  nextStepButtonTitle?: React.ReactNode;
  prevStepButtonTitle?: React.ReactNode;
  nextStepButtonDisabled?: boolean;
  onNext?: () => void;
  onPrev?: () => void;
  children: React.ReactNode;
  className?: string;
}

export function WizardLayout({
  currentStep,
  maxSteps,
  nextStepButtonTitle: _nextStepButtonTitle,
  prevStepButtonTitle: _prevStepButtonTitle,
  nextStepButtonDisabled: _nextStepButtonDisabled,
  onNext: _onNext,
  onPrev: _onPrev,
  children,
  className
}: WizardLayoutProps) {
  return (
    <div data-slot="wizard-layout" className={cn("flex flex-col gap-6", className)}>
      {/* Step indicator */}
      <div className="flex items-center justify-center">
        <Steps currentStep={currentStep} maxSteps={maxSteps} />
      </div>

      {/* Content */}
      <div className="flex flex-col gap-4">{children}</div>
    </div>
  );
}

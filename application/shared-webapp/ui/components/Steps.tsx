import { CheckIcon } from "lucide-react";

import { cn } from "../utils";

/**
 * Onboarding step indicator.
 * Ported from cal.com `packages/ui/components/step/Steps.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface StepsProps {
  /** Zero-based index of the current active step. */
  currentStep: number;
  /** Total number of steps. */
  maxSteps: number;
  stepLabel?: (current: number, total: number) => string;
  navigateToStep?: (step: number) => void;
  className?: string;
}

export function Steps({ currentStep, maxSteps, stepLabel, navigateToStep, className }: StepsProps) {
  return (
    <div data-slot="steps" className={cn("flex items-center gap-2", className)} role="list">
      {Array.from({ length: maxSteps }, (_, i) => {
        const isCompleted = i < currentStep;
        const isActive = i === currentStep;
        const isClickable = !!navigateToStep && i < currentStep;

        const label = stepLabel ? stepLabel(i + 1, maxSteps) : `Step ${i + 1} of ${maxSteps}`;

        return isClickable ? (
          <button
            key={i}
            type="button"
            aria-current={isActive ? "step" : undefined}
            aria-label={label}
            className={cn(
              "flex size-8 shrink-0 items-center justify-center rounded-full border-2 text-xs font-semibold transition-colors",
              isCompleted && "cursor-pointer border-primary bg-primary text-primary-foreground hover:border-primary/70"
            )}
            onClick={() => navigateToStep?.(i)}
          >
            {isCompleted ? <CheckIcon className="size-4" /> : <span>{i + 1}</span>}
          </button>
        ) : (
          <div
            key={i}
            role="listitem"
            aria-current={isActive ? "step" : undefined}
            aria-label={label}
            className={cn(
              "flex size-8 shrink-0 items-center justify-center rounded-full border-2 text-xs font-semibold transition-colors",
              isCompleted && "border-primary bg-primary text-primary-foreground",
              isActive && "border-primary bg-background text-primary",
              !isCompleted && !isActive && "border-muted-foreground/30 bg-background text-muted-foreground"
            )}
          >
            {isCompleted ? <CheckIcon className="size-4" /> : <span>{i + 1}</span>}
          </div>
        );
      })}
    </div>
  );
}

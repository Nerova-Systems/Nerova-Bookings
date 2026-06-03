import { t } from "@lingui/core/macro";
import { Button } from "@repo/ui/components/Button";
import { cn } from "@repo/ui/utils";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useState } from "react";

/**
 * Horizontal screenshot carousel for the app detail page. Mirrors cal.com's app detail slider: a
 * single visible frame with prev/next controls and clickable dots. Keeps the active index in local
 * state — there is no autoplay so it never moves without user intent.
 */
export function AppScreenshotCarousel({
  screenshots,
  appName
}: Readonly<{ screenshots: readonly string[]; appName: string }>) {
  const [activeIndex, setActiveIndex] = useState(0);

  if (screenshots.length === 0) return null;

  const clampedIndex = Math.min(activeIndex, screenshots.length - 1);
  const goPrevious = () => setActiveIndex((index) => (index <= 0 ? screenshots.length - 1 : index - 1));
  const goNext = () => setActiveIndex((index) => (index >= screenshots.length - 1 ? 0 : index + 1));

  return (
    <div className="flex flex-col gap-3">
      <div className="relative overflow-hidden rounded-lg border border-border bg-muted/30">
        <img
          src={screenshots[clampedIndex]}
          alt={t`Screenshot ${clampedIndex + 1} of ${appName}`}
          className="aspect-[16/10] w-full object-cover"
        />
        {screenshots.length > 1 && (
          <>
            <Button
              type="button"
              variant="secondary"
              size="icon"
              aria-label={t`Previous screenshot`}
              className="absolute top-1/2 left-3 -translate-y-1/2 rounded-full shadow-sm"
              onClick={goPrevious}
            >
              <ChevronLeftIcon className="size-4" />
            </Button>
            <Button
              type="button"
              variant="secondary"
              size="icon"
              aria-label={t`Next screenshot`}
              className="absolute top-1/2 right-3 -translate-y-1/2 rounded-full shadow-sm"
              onClick={goNext}
            >
              <ChevronRightIcon className="size-4" />
            </Button>
          </>
        )}
      </div>

      {screenshots.length > 1 && (
        <div className="flex justify-center gap-2" role="tablist" aria-label={t`Screenshots`}>
          {screenshots.map((screenshot, index) => (
            <button
              key={screenshot}
              type="button"
              role="tab"
              aria-selected={index === clampedIndex}
              aria-label={t`Go to screenshot ${index + 1}`}
              onClick={() => setActiveIndex(index)}
              className={cn(
                "h-2 rounded-full outline-ring transition-all focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
                index === clampedIndex ? "w-6 bg-primary" : "w-2 bg-muted-foreground/30 hover:bg-muted-foreground/50"
              )}
            />
          ))}
        </div>
      )}
    </div>
  );
}

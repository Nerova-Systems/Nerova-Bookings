import { t } from "@lingui/core/macro";
import { Button } from "@repo/ui/components/Button";
import { cn } from "@repo/ui/utils";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

interface AppSliderProps {
  title: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}

/**
 * Horizontal, scroll-snap slider used on the App Store landing page (Featured categories, Most
 * popular, Newly added). Mirrors cal.com's `AppStoreCategories`/`PopularAppsSlider`: a titled row
 * with prev/next arrows that page the track. Uses native CSS scroll-snap instead of a JS carousel
 * library so it stays light and accessible. Each child should be a fixed-width snap item.
 */
export function AppSlider({ title, children, className }: Readonly<AppSliderProps>) {
  const trackRef = useRef<HTMLDivElement>(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);

  const updateArrows = useCallback(() => {
    const track = trackRef.current;
    if (track === null) return;
    const { scrollLeft, scrollWidth, clientWidth } = track;
    setCanScrollLeft(scrollLeft > 1);
    setCanScrollRight(scrollLeft + clientWidth < scrollWidth - 1);
  }, []);

  useEffect(() => {
    const track = trackRef.current;
    if (track === null) return;
    updateArrows();
    const observer = new ResizeObserver(updateArrows);
    observer.observe(track);
    return () => observer.disconnect();
  }, [updateArrows]);

  const scrollByPage = (direction: -1 | 1) => {
    const track = trackRef.current;
    if (track === null) return;
    track.scrollBy({ left: direction * track.clientWidth * 0.9, behavior: "smooth" });
  };

  return (
    <section className={cn("flex flex-col gap-4", className)}>
      <div className="flex items-center justify-between gap-4">
        <h2 className="text-base font-semibold text-foreground">{title}</h2>
        <div className="flex gap-2">
          <Button
            type="button"
            variant="outline"
            size="icon"
            aria-label={t`Scroll left`}
            disabled={!canScrollLeft}
            onClick={() => scrollByPage(-1)}
          >
            <ChevronLeftIcon className="size-4" />
          </Button>
          <Button
            type="button"
            variant="outline"
            size="icon"
            aria-label={t`Scroll right`}
            disabled={!canScrollRight}
            onClick={() => scrollByPage(1)}
          >
            <ChevronRightIcon className="size-4" />
          </Button>
        </div>
      </div>
      <div
        ref={trackRef}
        onScroll={updateArrows}
        className="no-scrollbar -mx-1 flex snap-x snap-mandatory gap-3 overflow-x-auto scroll-smooth px-1 pb-1"
      >
        {children}
      </div>
    </section>
  );
}

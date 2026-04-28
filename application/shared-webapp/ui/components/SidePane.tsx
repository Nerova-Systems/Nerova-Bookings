import type * as React from "react";

import { useRouterState } from "@tanstack/react-router";
import { XIcon } from "lucide-react";
import { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";

import { cn } from "../utils";
import { Button } from "./Button";

// Context so nested children (Header / Close / etc) can request the pane close without threading props.
interface SidePaneContextValue {
  onClose: () => void;
  needsFullscreen: boolean;
}
const SidePaneContext = createContext<SidePaneContextValue | null>(null);

function useSidePaneContext() {
  const context = useContext(SidePaneContext);
  if (!context) {
    throw new Error("SidePane components must be used within a SidePane.");
  }
  return context;
}

// Minimum content width for side-by-side layout (main + 24rem side pane).
const SIDE_PANE_WIDTH_REM = 24;
const MIN_SIDE_BY_SIDE_WIDTH_REM = SIDE_PANE_WIDTH_REM * 2;

// When the viewport is too narrow for side-by-side, the pane switches to a full-screen drawer with a backdrop.
function useNeedsFullscreen() {
  const [needsFullscreen, setNeedsFullscreen] = useState(false);

  useEffect(() => {
    const rootFontSize = () => Number.parseFloat(getComputedStyle(document.documentElement).fontSize);
    const check = () => {
      setNeedsFullscreen(window.innerWidth < MIN_SIDE_BY_SIDE_WIDTH_REM * rootFontSize());
    };
    check();
    window.addEventListener("resize", check);
    return () => window.removeEventListener("resize", check);
  }, []);

  return needsFullscreen;
}

function useSidePaneAccessibility(
  isOpen: boolean,
  onClose: () => void,
  needsFullscreen: boolean,
  paneRef: React.RefObject<HTMLElement | null>
) {
  const previouslyFocusedElement = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (isOpen && needsFullscreen) {
      previouslyFocusedElement.current = document.activeElement as HTMLElement;
    }
  }, [isOpen, needsFullscreen]);

  useEffect(() => {
    if (!isOpen && previouslyFocusedElement.current) {
      previouslyFocusedElement.current.focus();
      previouslyFocusedElement.current = null;
    }
  }, [isOpen]);

  // Only lock body scroll in fullscreen mode — wide-viewport docked panes coexist with scrolling content.
  useEffect(() => {
    if (isOpen && needsFullscreen) {
      const originalOverflow = document.body.style.overflow;
      document.body.style.overflow = "hidden";
      return () => {
        document.body.style.overflow = originalOverflow;
      };
    }
  }, [isOpen, needsFullscreen]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  // Focus trap only in fullscreen mode (docked mode shares focus with surrounding content).
  useEffect(() => {
    if (!isOpen || !needsFullscreen || !paneRef.current) {
      return;
    }
    const focusable = paneRef.current.querySelectorAll<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    );
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const handleTab = (event: KeyboardEvent) => {
      if (event.key !== "Tab") return;
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    };
    document.addEventListener("keydown", handleTab);
    return () => document.removeEventListener("keydown", handleTab);
  }, [isOpen, needsFullscreen, paneRef]);
}

type WindowWithTracking = {
  __trackInteraction?: (name: string, type: string, action: string, extraProperties?: Record<string, string>) => void;
};

let pendingCloseTimer: ReturnType<typeof setTimeout> | undefined;

interface SidePaneProps {
  children: React.ReactNode;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  trackingTitle: string;
  trackingKey?: string;
  className?: string;
  "aria-label"?: string;
}

function SidePane({
  children,
  isOpen,
  onOpenChange,
  trackingTitle,
  trackingKey,
  className,
  "aria-label": ariaLabel
}: Readonly<SidePaneProps>) {
  const paneRef = useRef<HTMLElement>(null);
  const needsFullscreen = useNeedsFullscreen();
  const prevOpen = useRef(false);
  const prevKey = useRef(trackingKey);
  const isOpenRef = useRef(isOpen);
  isOpenRef.current = isOpen;

  // Auto-close when the user navigates to a different pathname/hash. Consumers whose pane visibility
  // is derived state (not a boolean they can flip) still benefit — the pane unmounts internally even
  // if `isOpen` stays true until the consumer clears its source state on the next interaction.
  const currentLocation = useRouterState({ select: (s) => `${s.location.pathname}${s.location.hash}` });
  const openedAtLocationRef = useRef<string | null>(null);
  const [closedByNavigation, setClosedByNavigation] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      openedAtLocationRef.current = null;
      setClosedByNavigation(false);
      return;
    }
    if (openedAtLocationRef.current === null) {
      openedAtLocationRef.current = currentLocation;
    } else if (openedAtLocationRef.current !== currentLocation) {
      setClosedByNavigation(true);
      onOpenChange(false);
    }
  }, [isOpen, currentLocation, onOpenChange]);

  useEffect(() => {
    const opened = isOpen && !prevOpen.current;
    const contentChanged = isOpen && prevOpen.current && trackingKey !== undefined && trackingKey !== prevKey.current;
    if (opened || contentChanged) {
      (window as unknown as WindowWithTracking).__trackInteraction?.(trackingTitle, "sidepane", "Open");
    }
    if (!isOpen && prevOpen.current) {
      (window as unknown as WindowWithTracking).__trackInteraction?.(trackingTitle, "sidepane", "Close");
    }
    prevOpen.current = isOpen;
    prevKey.current = trackingKey;
  }, [isOpen, trackingTitle, trackingKey]);

  useEffect(() => {
    clearTimeout(pendingCloseTimer);
    pendingCloseTimer = undefined;
    return () => {
      if (isOpenRef.current) {
        const title = trackingTitle;
        pendingCloseTimer = setTimeout(() => {
          (window as unknown as WindowWithTracking).__trackInteraction?.(title, "sidepane", "Close");
          pendingCloseTimer = undefined;
        }, 100);
      }
    };
  }, [trackingTitle]);

  const onClose = useCallback(() => {
    onOpenChange(false);
  }, [onOpenChange]);

  useSidePaneAccessibility(isOpen, onClose, needsFullscreen, paneRef);

  if (!isOpen || closedByNavigation) {
    return null;
  }

  // Wide viewport: SidePane renders inline (a regular flex child in AppLayout's row) so the main column
  // naturally shrinks to make room for it. The AppLayout keeps the sidePane slot at its intrinsic width.
  // Narrow viewport: the pane full-bleeds with a backdrop so it overlays everything.
  return (
    <SidePaneContext.Provider value={{ onClose, needsFullscreen }}>
      {needsFullscreen && (
        <button
          type="button"
          aria-label="Close side panel"
          tabIndex={-1}
          onClick={onClose}
          className="fixed top-[calc(var(--past-due-banner-height,0rem)+var(--invitation-banner-height,0rem))] right-0 bottom-0 left-0 z-[35] bg-black/50"
        />
      )}
      <aside
        ref={paneRef}
        role="region"
        className={cn(
          "flex h-full shrink-0 flex-col bg-card",
          needsFullscreen
            ? "fixed top-[calc(var(--past-due-banner-height,0rem)+var(--invitation-banner-height,0rem))] right-0 bottom-0 left-0 z-40"
            : "w-[var(--side-pane-width,24rem)] border-l border-border",
          className
        )}
        aria-label={ariaLabel}
      >
        {children}
      </aside>
    </SidePaneContext.Provider>
  );
}

interface SidePaneHeaderProps {
  children: React.ReactNode;
  className?: string;
  showCloseButton?: boolean;
  closeButtonLabel?: string;
}

function SidePaneHeader({
  children,
  className,
  showCloseButton = true,
  closeButtonLabel = "Close"
}: Readonly<SidePaneHeaderProps>) {
  const { onClose } = useSidePaneContext();

  return (
    <div className={cn("relative flex h-16 shrink-0 items-center px-4", className)}>
      <h4 className="flex h-full items-center">{children}</h4>
      {showCloseButton && (
        <Button
          variant="ghost"
          size="icon-sm"
          onClick={onClose}
          className="absolute top-4 right-4"
          aria-label={closeButtonLabel}
        >
          <XIcon className="size-6" />
        </Button>
      )}
    </div>
  );
}

interface SidePaneBodyProps {
  children: React.ReactNode;
  className?: string;
}

function SidePaneBody({ children, className }: Readonly<SidePaneBodyProps>) {
  return <div className={cn("flex-1 overflow-y-auto p-4", className)}>{children}</div>;
}

interface SidePaneFooterProps {
  children: React.ReactNode;
  className?: string;
}

function SidePaneFooter({ children, className }: Readonly<SidePaneFooterProps>) {
  return <div className={cn("mt-auto p-4 pb-[max(1rem,env(safe-area-inset-bottom))]", className)}>{children}</div>;
}

function SidePaneClose({ children, className, ...props }: React.ComponentProps<typeof Button>) {
  const { onClose } = useSidePaneContext();

  return (
    <Button onClick={onClose} className={className} {...props}>
      {children}
    </Button>
  );
}

export { SidePane, SidePaneBody, SidePaneClose, SidePaneFooter, SidePaneHeader };

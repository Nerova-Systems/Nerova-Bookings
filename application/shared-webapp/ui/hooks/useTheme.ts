import { useCallback, useEffect, useSyncExternalStore } from "react";

type Theme = "light" | "dark" | "system";

const STORAGE_KEY = "app-theme";

function getStoredTheme(): Theme {
  if (typeof localStorage === "undefined") return "system";
  return (localStorage.getItem(STORAGE_KEY) as Theme) ?? "system";
}

function resolveTheme(theme: Theme): "light" | "dark" {
  if (theme !== "system") return theme;
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

const listeners = new Set<() => void>();

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function notifyListeners() {
  listeners.forEach((l) => l());
}

/**
 * Headless theme hook. Reads/writes the current app theme from `localStorage`.
 * Applies the resolved theme as a `data-theme` attribute on `<html>`.
 * Responds to system preference changes when theme is `"system"`.
 *
 * Ported from cal.com `packages/lib/hooks/useTheme.ts` (cf2a55c).
 *
 * Deviation: cal.com delegates to `next-themes`. Nerova uses browser `localStorage`
 * directly since the project uses TanStack Router (no Next.js runtime). The embed
 * theme override from cal.com's embed-core is not reproduced here; downstream
 * embed consumers should call `setTheme` explicitly.
 */
export function useTheme() {
  const theme = useSyncExternalStore(subscribe, getStoredTheme, () => "system" as Theme);

  const resolvedTheme = typeof window !== "undefined" ? resolveTheme(theme) : "light";

  const setTheme = useCallback((newTheme: Theme) => {
    localStorage.setItem(STORAGE_KEY, newTheme);
    notifyListeners();
  }, []);

  // Keep <html data-theme="..."> in sync.
  useEffect(() => {
    const root = document.documentElement;
    root.dataset["theme"] = resolvedTheme;
  }, [resolvedTheme]);

  // Re-sync on system preference changes.
  useEffect(() => {
    if (theme !== "system") return;
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = () => notifyListeners();
    mq.addEventListener("change", handler);
    return () => mq.removeEventListener("change", handler);
  }, [theme]);

  return { theme, resolvedTheme, setTheme };
}

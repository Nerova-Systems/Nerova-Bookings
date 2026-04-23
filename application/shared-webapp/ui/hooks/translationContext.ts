import { createContext } from "react";

// Defined in @repo/ui so date-aware components (Calendar, DatePicker, NumberField, etc.) can read
// the current locale without importing from @repo/infrastructure (TS rootDir boundary). Infra's
// TranslationProvider populates the value at runtime; the default falls back to <html lang>.
export type TranslationContextValue = {
  currentLocale: string;
  setLocale: (locale: string) => Promise<void>;
  locales: string[];
  getLocaleInfo(locale: string): { label: string; locale: string; territory: string; rtl: boolean };
};

export const translationContext = createContext<TranslationContextValue>({
  currentLocale: typeof document !== "undefined" ? document.documentElement.lang : "en-US",
  setLocale: async (locale) => {
    document.dispatchEvent(new CustomEvent("locale-change-request", { detail: { locale } }));
  },
  locales: [],
  getLocaleInfo: () => {
    throw new Error("Not initialized.");
  }
});
